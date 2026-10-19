using System.Buffers.Binary;
using System.Security.Cryptography;
using ManagedBass;
using osu.Game.Rulesets.Shred;

internal static class Revision63AudioChecks
{
    // Hashes of the PCM data in the user's original attachments, excluding RIFF metadata.
    private static readonly Dictionary<string, string> drumPcm = new()
    {
        ["drum-hitnormal"] = "a1f994cba90681953ff4edcdb9a103862bd01c7be23a9a423a3708ec6fae5153",
        ["drum-hitwhistle"] = "329c92029ef0287fb9cfa2ee4f9b5a7699f8623b9136721f7974bfe0a51c2c72",
        ["drum-hitclap"] = "d553bee6144866e2bda9b36c49f3a20da8242790c3526cd8ebca5a3078e777bd",
        ["drum-hitfinish"] = "f8309f35369065f2f7f8af171d729f78d23c0d01e1ebef3ac1b386c71862e50c"
    };

    public static byte[] Pcm(byte[] wav)
    {
        for (int offset = 12; offset + 8 <= wav.Length;)
        {
            int size = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(offset + 4));
            if (wav.AsSpan(offset, 4).SequenceEqual("data"u8))
                return wav.AsSpan(offset + 8, size).ToArray();
            offset += 8 + size + (size & 1);
        }
        throw new Exception("No WAV data chunk");
    }

    private static void check(bool condition, string message)
    {
        if (!condition) throw new Exception(message + "; BASS=" + Bass.LastError);
        Console.WriteLine("PASS v63 " + message);
    }

    public static void Run()
    {
        check(Bass.Init(0), "BASS initialises without an output device");
        try
        {
            using var resources = new ShredRuleset().CreateResourceStore();
            foreach (var file in Revision45Checks.Files)
            {
                byte[] wav = resources.Get("Samples/Gameplay/" + file.name + ".wav");
                byte[] pcm = Pcm(wav);
                check(Convert.ToHexString(SHA256.HashData(wav)).Equals(file.hash, StringComparison.OrdinalIgnoreCase),
                    file.name + " expected bundled bytes (Normal/Soft unchanged)");
                if (drumPcm.TryGetValue(file.name, out string expected))
                {
                    check(wav.Length == pcm.Length + 44 && wav.AsSpan(12, 4).SequenceEqual("fmt "u8)
                        && wav.AsSpan(36, 4).SequenceEqual("data"u8), file.name + " minimal PCM WAV header");
                    check(Convert.ToHexString(SHA256.HashData(pcm)).Equals(expected, StringComparison.OrdinalIgnoreCase),
                        file.name + " original PCM retained exactly: no normalisation, gain, resampling or compression");
                }

                // Same sample loading flags as osu-framework's SampleBassFactory.
                int sample = Bass.SampleLoad(wav, 0, wav.Length, 2, BassFlags.SampleOverrideLongestPlaying);
                check(sample != 0, file.name + " loads using framework flags");
                try
                {
                    byte[] decoded = new byte[pcm.Length];
                    check(Bass.SampleGetData(sample, decoded) && decoded.SequenceEqual(pcm), file.name + " native BASS decode is bit-exact");
                    // Stream sample channels are the path used by lazer, including on Windows.
                    var streams = new List<int>();
                    try
                    {
                        for (int i = 0; i < 12; i++)
                        {
                            int channel = Bass.SampleGetChannel(sample, BassFlags.SampleChannelStream | BassFlags.Decode);
                            check(channel != 0, file.name + " stream channel " + i);
                            streams.Add(channel);
                        }
                        foreach (int channel in streams)
                            check(Bass.ChannelGetData(channel, decoded, decoded.Length) == pcm.Length && decoded.SequenceEqual(pcm),
                                file.name + " complete independent stream retains waveform");
                    }
                    finally { foreach (int channel in streams) Bass.StreamFree(channel); }
                }
                finally { Bass.SampleFree(sample); }
            }
        }
        finally { Bass.Free(); }
    }
}
