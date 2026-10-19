#if SHRED_OFFICIAL
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using osu.Game.Audio;
using LegacyHitSampleInfo = osu.Game.Rulesets.Objects.Legacy.ConvertHitObjectParser.LegacyHitSampleInfo;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;

namespace osu.Game.Rulesets.Shred.Compatibility
{
    // A versioned filename envelope survives the official legacy encoder, undo
    // patches, exports and reimports. It is removed before gameplay sample lookup.
    public static class ShredEditorCodec
    {
        public const string Prefix = "shred-native-v1-";
        // Filename samples are immutable. Keep the parsed envelope for its lifetime,
        // without retaining old undo states or every beatmap ever opened.
        private static readonly ConditionalWeakTable<HitSampleInfo, Payload> decoded = new();

        private sealed class Payload
        {
            public int Row;
            public int Fret;
            public ShredPracticeArticulation Articulation;
            public double Duration;
            // Historical v1 field name. Since v46 it contains every playable sample:
            // the legacy encoder cannot retain auto-addition banks alongside a filename carrier.
            // Older v1 readers already expand this list without filtering sample names.
            public List<Sample> NormalSamples = new List<Sample>();
        }

        private sealed class Sample
        {
            public string Name;
            public string Bank;
            public string Suffix;
            public int Volume;
            public bool AutoBank;
            public bool Beatmap;
            public string File;
            public int? CustomBank;
            public bool Layered;

            public static Sample From(HitSampleInfo sample) => new Sample
            {
                Name = sample.Name, Bank = sample.Bank, Suffix = sample.Suffix,
                Volume = sample.Volume, AutoBank = sample.EditorAutoBank,
                Beatmap = sample.UseBeatmapSamples,
                File = (sample as ConvertHitObjectParser.FileHitSampleInfo)?.Filename,
                CustomBank = (sample as LegacyHitSampleInfo)?.CustomSampleBank,
                Layered = (sample as LegacyHitSampleInfo)?.IsLayered ?? false
            };

            public HitSampleInfo ToSample()
            {
                if (File != null) return new ConvertHitObjectParser.FileHitSampleInfo(File, Volume);
                if (CustomBank.HasValue)
                    return new LegacyHitSampleInfo(Name, Bank, Volume, AutoBank, CustomBank.Value, Layered);
                return new HitSampleInfo(Name, Bank, Suffix, Volume, AutoBank, Beatmap);
            }
        }

        private static bool isMarker(HitSampleInfo sample)
            => sample is ConvertHitObjectParser.FileHitSampleInfo file
                && file.Filename.StartsWith("shred-native-", StringComparison.Ordinal);

        public static bool HasNativeMarker(HitObject source) => source.Samples.Any(isMarker);

        private static Payload read(HitSampleInfo sample)
            => decoded.GetValue(sample, parse);

        private static Payload parse(HitSampleInfo sample)
        {
            string filename = ((ConvertHitObjectParser.FileHitSampleInfo)sample).Filename;
            if (!filename.StartsWith(Prefix, StringComparison.Ordinal))
                throw new FormatException("Version de carte Shred non prise en charge.");
            if (filename.Length > 65536) throw new FormatException("Marqueur Shred trop long.");
            string encoded = filename.Substring(Prefix.Length).Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
            var data = JsonConvert.DeserializeObject<Payload>(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)))
                ?? throw new FormatException("Marqueur Shred vide.");
            if (data.Row < 0 || data.Row > 1 || data.Fret < 0 || data.Fret > 5
                || !Enum.IsDefined(data.Articulation) || !double.IsFinite(data.Duration) || data.Duration < 0
                || data.NormalSamples == null || data.NormalSamples.Count > 64)
                throw new FormatException("Données de note Shred invalides.");
            if (data.NormalSamples.Any(s => s.File?.StartsWith("shred-native-", StringComparison.Ordinal) == true))
                throw new FormatException("Marqueur Shred imbriqué.");
            return data;
        }

        public static IList<HitSampleInfo> PlayableSamples(IEnumerable<HitSampleInfo> samples)
        {
            var result = new List<HitSampleInfo>();
            foreach (var sample in samples)
            {
                if (isMarker(sample)) result.AddRange(read(sample).NormalSamples.Select(s => s.ToSample()));
                else result.Add(sample.With());
            }
            // Some historical native envelopes contain 16 identical ordinary
            // bank samples per note. They create 16 channels and saturate the mix.
            // Remove exact duplicates only: different banks, volumes, lookup
            // indices and layering flags are meaningful and must survive.
            // Explicit filename samples are not part of this legacy corruption.
            if (result.Count > 1)
            {
                var seen = new HashSet<(Type, string, string, string, int, bool, bool, int?, bool)>();
                result.RemoveAll(sample =>
                {
                    if (!Skinning.ShredDefaultSkinTransformer.HasBundledSample(sample)) return false;
                    var legacy = sample as LegacyHitSampleInfo;
                    return !seen.Add((sample.GetType(), sample.Name, sample.Bank, sample.Suffix,
                        sample.Volume, sample.EditorAutoBank, sample.UseBeatmapSamples,
                        legacy?.CustomSampleBank, legacy?.IsLayered ?? false));
                });
            }
            return result;
        }

        public static void Synchronise(ShredHitObject note, IEnumerable<HitSampleInfo> editedSamples = null)
        {
            // PreProcess visits the entire map after an edit. Most envelopes are
            // already current; do not decode and serialise them all over again.
            if (editedSamples == null && note.Samples.Count == 1 && isMarker(note.Samples[0]))
            {
                var current = read(note.Samples[0]);
                if (current.Row == note.Row && current.Fret == note.Fret
                    && current.Articulation == note.Articulation && current.Duration == note.Duration)
                    return;
            }
            var playable = PlayableSamples(editedSamples ?? note.Samples);
            var data = new Payload
            {
                Row = note.Row, Fret = note.Fret, Articulation = note.Articulation,
                Duration = note.Duration,
                NormalSamples = playable.Select(Sample.From).ToList()
            };
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data)))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            string filename = Prefix + encoded;
            var existing = note.Samples.OfType<ConvertHitObjectParser.FileHitSampleInfo>().FirstOrDefault(isMarker);
            if (existing?.Filename == filename) return;
            note.Samples = new List<HitSampleInfo>
            {
                new ConvertHitObjectParser.FileHitSampleInfo(filename, playable.FirstOrDefault()?.Volume ?? 100)
            };
        }

        public static bool TryDecode(HitObject source, out ShredHitObject note)
        {
            note = null;
            // A live Shred object is authoritative; its marker may precede a mutation.
            if (source is ShredHitObject) return false;
            var markers = source.Samples.Where(isMarker).ToArray();
            if (markers.Length == 0) return false;
            if (markers.Length != 1) throw new FormatException("Plusieurs marqueurs sur une note Shred.");
            var data = read(markers[0]);
            note = new ShredHitObject
            {
                StartTime = source.StartTime, Duration = data.Duration,
                Row = data.Row, Fret = data.Fret, Articulation = data.Articulation,
                Samples = PlayableSamples(source.Samples)
            };
            return true;
        }
    }
}
#endif
