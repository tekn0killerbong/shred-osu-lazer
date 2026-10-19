using System.IO.Compression;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Compatibility;
using osu.Game.Rulesets.Shred.Objects;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;

internal static class Revision43ExportChecks
{
    private static string Fingerprint(IBeatmap map) => string.Join(";", map.HitObjects.Cast<ShredHitObject>()
        .Select(n=>$"{n.StartTime:R}/{n.Duration:R}/{n.Row}/{n.Fret}/{n.Articulation}"));
    private static IBeatmap Decode(Stream stream)
    {
        using var reader=new LineBufferedReader(stream);
        var map=Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
        if(!ShredAutoImport.IsAuthored(map)) throw new Exception("Export lost native identification");
        return new FlatWorkingBeatmap(map).GetPlayableBeatmap(new ShredRuleset().RulesetInfo);
    }
    public static async Task Run(BeatmapManager manager, BeatmapInfo info, string folder, string archive, string output)
    {
        string expected;
        using(var zip=ZipFile.OpenRead(archive))
        using(var source=zip.Entries.Single(e=>e.FullName.EndsWith(".osu")).Open()) expected=Fingerprint(Decode(source));
        await manager.ExportLegacy(info.BeatmapSet);
        await manager.ExportLegacy(info);
        string exports=Path.Combine(folder,"exports");
        var osz=Directory.GetFiles(exports,"*.osz").Single();
        var osu=Directory.GetFiles(exports,"*.osu").Single();
        using(var zip=ZipFile.OpenRead(osz))
        {
            using(var stream=zip.Entries.Single(e=>e.FullName.EndsWith(".osu")).Open())
                if(Fingerprint(Decode(stream))!=expected) throw new Exception("OSZ lost note data");
            using var original=ZipFile.OpenRead(archive);
            foreach(var entry in original.Entries.Where(e=>!e.FullName.EndsWith(".osu")))
            {
                using var a=entry.Open();using var b=zip.GetEntry(entry.FullName)!.Open();
                using var aa=new MemoryStream();using var bb=new MemoryStream();a.CopyTo(aa);b.CopyTo(bb);
                if(!aa.ToArray().SequenceEqual(bb.ToArray())) throw new Exception("Export resource changed: "+entry.FullName);
            }
        }
        using(var stream=File.OpenRead(osu)) if(Fingerprint(Decode(stream))!=expected) throw new Exception("OSU lost note data");
        if(manager.QueryBeatmap(b=>b.ID==info.ID)!.Ruleset.ShortName!="shred") throw new Exception("Export altered library classification");
        Directory.CreateDirectory(output);
        File.Copy(osz,Path.Combine(output,"blink-182-Happy-Holidays-shred.osz"),true);
        File.Copy(osu,Path.Combine(output,"blink-182-Happy-Holidays-shred.osu"),true);
        Console.WriteLine("PASS official manager OSZ and OSU exports retain all notes, fractional timings, durations, rows, articulations, audio, background and native classification");
    }
}
