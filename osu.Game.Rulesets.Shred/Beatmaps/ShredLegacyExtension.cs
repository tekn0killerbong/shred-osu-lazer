using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
namespace osu.Game.Rulesets.Shred
{
	public partial class ShredRuleset
	{
		public int LegacyBaseMode => 3;
		public int LegacyColumns => 12;
		public void EncodeRulesetData(TextWriter writer, IBeatmap beatmap)
		{
			writer.WriteLine("Version:2");
			// The legacy encoder reduces AudioFilename to its basename. Preserve the
			// complete relative resource path, including audio subdirectories.
			writer.WriteLine("AudioFile:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(beatmap.Metadata.AudioFile ?? string.Empty)));
			for (int index = 0; index < beatmap.HitObjects.Count; index++)
			{
				if (beatmap.HitObjects[index] is not ShredHitObject note) throw new InvalidDataException("Expected Shred notes.");
				validate(note);
				writer.WriteLine(FormattableString.Invariant($"{index},{(int)note.Articulation},{note.Duration}"));
			}
		}
		public void DecodeRulesetData(Beatmap beatmap, IReadOnlyList<HitObject> originalOrder, IReadOnlyList<string> lines)
		{
			if (beatmap.Difficulty.CircleSize != 12 || lines.Count == 0 || (lines[0] != "Version:1" && lines[0] != "Version:2"))
				throw new FormatException("Unsupported Shred format or column count.");
			bool version2 = lines[0] == "Version:2";
			string audioFile = null;
			Dictionary<int, double> durations = new Dictionary<int, double>();
			Dictionary<int, ShredPracticeArticulation> types = new Dictionary<int, ShredPracticeArticulation>();
			foreach (string line in lines.Skip(1))
			{
				if (version2 && line.StartsWith("AudioFile:", StringComparison.Ordinal))
				{
					if (audioFile != null) throw new FormatException("Duplicate Shred audio path.");
					audioFile = Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(10)));
					continue;
				}
				string[] parts = line.Split(',');
				if (parts.Length != (version2 ? 3 : 2) || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
					|| index < 0 || index >= originalOrder.Count || !int.TryParse(parts[1], out int type) || type < 0 || type > (version2 ? 4 : 3) || !types.TryAdd(index, (ShredPracticeArticulation)type))
					throw new FormatException("Invalid Shred articulation data.");
				if (version2)
				{
					if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double duration) || !double.IsFinite(duration) || duration < 0)
						throw new FormatException("Invalid Shred duration.");
					durations.Add(index, duration);
				}
			}
			List<HitObject> notes = new List<HitObject>();
			for (int index = 0; index < originalOrder.Count; index++)
			{
				HitObject original = originalOrder[index];
				if (original is not IHasXPosition position || (!version2 && original is IHasDuration) || original is IHasPath)
					throw new FormatException("Unsupported Shred object geometry.");
				int column = Math.Clamp((int)(position.X * 12 / 512), 0, 11);
				if (!types.TryGetValue(index, out ShredPracticeArticulation articulation)) throw new FormatException("Missing Shred articulation.");
				ShredHitObject note = new ShredHitObject { StartTime = original.StartTime, Samples = original.Samples, Row = column / 6, Fret = column % 6, Articulation = articulation == ShredPracticeArticulation.HammerOn || articulation == ShredPracticeArticulation.PullOff ? ShredPracticeArticulation.HammerPull : articulation, Duration = version2 ? durations[index] : 0 };
				validate(note);
				notes.Add(note);
			}
			beatmap.HitObjects = notes;
			if (audioFile != null) beatmap.Metadata.AudioFile = audioFile;
		}
		public void CopyRulesetData(HitObject source, HitObject target)
		{
			if (source is ShredHitObject from && target is ShredHitObject to)
			{
				to.Row = from.Row;
				to.Fret = from.Fret;
				to.Articulation = from.Articulation;
				to.Duration = from.Duration;
			}
		}
		private static void validate(ShredHitObject note)
		{
			if (note.Row < 0 || note.Row > 1 || note.Fret < 0 || note.Fret > 5 || !Enum.IsDefined(note.Articulation)
				|| !double.IsFinite(note.Duration) || note.Duration < 0
				|| (note.IsWham && (!note.IsLong || note.Row != 0 || note.Fret != 0))
				|| (!note.IsWham && note.Fret == 0 && note.IsLong))
				throw new FormatException("Invalid Shred note.");
		}
	}
}
