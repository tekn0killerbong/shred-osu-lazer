using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Shred.Beatmaps
{
    // Only osu!standard carriers can be selected as converted maps in official
    // song select. Mania carriers force mania before our converter is consulted.
    // Native exports are routed by their explicit CustomRuleset marker. Other
    // legacy maps still use the inherited official parser and source version.
    public sealed class ShredPortableDecoder : LegacyBeatmapDecoder
    {
        public const string Magic = "shred portable format v1";
        private readonly bool nativeInput;
        public new static void Register()
        {
            AddDecoder<Beatmap>(Magic, _ => new ShredPortableDecoder());
            AddDecoder<Beatmap>("osu file format v", header => new ShredPortableDecoder(int.Parse(header.Split('v').Last(), CultureInfo.InvariantCulture), true));
        }
        public ShredPortableDecoder(int version = 128, bool nativeInput = false) : base(version)
        {
            this.nativeInput = nativeInput;
        }

        protected override void ParseStreamInto(LineBufferedReader stream, bool isPrimaryStream, Beatmap output)
        {
            if (!isPrimaryStream) { base.ParseStreamInto(stream, false, output); return; }
            var source = new StringBuilder();
            string sourceLine;
            string sourceSection = "";
            bool nativeShred = false;
            while ((sourceLine = stream.ReadLine()) != null)
            {
                source.AppendLine(sourceLine);
                string trimmedSource = sourceLine.Trim().TrimStart('\uFEFF');
                if (trimmedSource.StartsWith("[", StringComparison.Ordinal)) sourceSection = trimmedSource;
                if (sourceSection == "[General]" && trimmedSource.StartsWith("CustomRuleset:", StringComparison.Ordinal))
                    nativeShred = trimmedSource.Substring(trimmedSource.IndexOf(':') + 1).Trim() == "shred";
            }
            using var sourceBytes = new MemoryStream(Encoding.UTF8.GetBytes(source.ToString()));
            using var input = new LineBufferedReader(sourceBytes);
            if (nativeInput && !nativeShred)
            {
                // Preserve the official parser and original format version for every other map.
                base.ParseStreamInto(input, true, output);
#if SHRED_OFFICIAL
                for (int i = 0; i < output.HitObjects.Count; i++)
                {
                    if (Compatibility.ShredEditorCodec.TryDecode(output.HitObjects[i], out var authored))
                        output.HitObjects[i] = ShredCarrierNote.From(authored);
                }
				if (Compatibility.ShredAutoImport.IsAuthored(output))
				{
					output.Difficulty.CircleSize = 12;
					// Mode 3 preserves effect scroll speeds in the stock legacy codec.
					// Only after decoding verified Shred data, restore the standard
					// carrier identity used by song select and automatic classification.
					base.ParseLine(output, Section.General, "Mode:0", true);
				}
#endif
                return;
            }
            var legacy = new StringBuilder();
            var data = new List<string>();
            var times = new List<double>();
            string section = "";
            bool marker = false, custom = false, mode3 = false, columns12 = false;
            string line;
            while ((line = input.ReadLine()) != null)
            {
                string trimmed = line.Trim().TrimStart('\uFEFF');
                if (trimmed == Magic || (nativeInput && trimmed == "osu file format v128")) { marker = true; legacy.AppendLine("osu file format v128"); continue; }
                if (trimmed.StartsWith("[", StringComparison.Ordinal)) section = trimmed;
                if (section == "[RulesetData]")
                {
                    if (!trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.Length > 0 && !trimmed.StartsWith("//", StringComparison.Ordinal)) data.Add(trimmed == "Version:1 [shred v29]" ? "Version:1"
                        : trimmed == "Version:2 [shred v29]" ? "Version:2" : trimmed);
                    continue;
                }
                if (section == "[General]" && trimmed.StartsWith("CustomRuleset:", StringComparison.Ordinal))
                {
                    custom = trimmed.Substring(trimmed.IndexOf(':') + 1).Trim() == "shred";
                    continue;
                }
                if (section == "[General]" && trimmed.StartsWith("Mode:", StringComparison.Ordinal))
                {
                    mode3 = trimmed.Substring(trimmed.IndexOf(':') + 1).Trim() == "3";
                }
                if (section == "[Difficulty]" && trimmed.StartsWith("CircleSize:", StringComparison.Ordinal))
                    columns12 = double.TryParse(trimmed.Substring(trimmed.IndexOf(':') + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double columns) && columns == 12;
                if (section == "[Metadata]" && (trimmed.StartsWith("BeatmapID:", StringComparison.Ordinal) || trimmed.StartsWith("BeatmapSetID:", StringComparison.Ordinal)))
                    line = trimmed.Split(':')[0] + ":-1";
                if (section == "[HitObjects]" && trimmed.Length > 0 && !trimmed.StartsWith("[", StringComparison.Ordinal) && !trimmed.StartsWith("//", StringComparison.Ordinal))
                    times.Add(double.Parse(trimmed.Split(',')[2], CultureInfo.InvariantCulture));
                legacy.AppendLine(line);
            }
            if (!marker || !custom || !mode3 || !columns12 || data.Count == 0) throw new FormatException("Not a portable shred v1 map.");
            using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(legacy.ToString()));
            using var reader = new LineBufferedReader(bytes);
            base.ParseStreamInto(reader, true, output);
            if (output.BeatmapInfo.Ruleset.OnlineID != 3 || output.HitObjects.Count != times.Count)
                throw new FormatException("Expected the shred native export carrier.");
			// Decode effects with their original mode, then expose the verified
			// Shred content through the standard-compatible catalogue carrier.
			base.ParseLine(output, Section.General, "Mode:0", true);
            output.Difficulty.CircleSize = 12;
            var original = new HitObject[times.Count];
            int sorted = 0;
            foreach (int index in Enumerable.Range(0, times.Count).OrderBy(i => times[i])) original[index] = output.HitObjects[sorted++];
            new ShredRuleset().DecodeRulesetData(output, original, data);
            output.HitObjects = output.HitObjects.Cast<osu.Game.Rulesets.Shred.Objects.ShredHitObject>()
                .OrderBy(n => n.StartTime).Select(n => (HitObject)ShredCarrierNote.From(n)).ToList();
        }
    }
}
