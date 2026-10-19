using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
namespace osu.Game.Rulesets.Shred.Beatmaps
{
	// Experimental Shred loader: standard mania lines plus an explicit extension section.
	// Standard lazer's .osu importer does not load [ShredArticulations].
	public static class ShredOsuReader
	{
		public static Beatmap<ShredHitObject> Read(TextReader reader, RulesetInfo ruleset)
		{
			Beatmap<ShredHitObject> beatmap = new Beatmap<ShredHitObject>();
			beatmap.BeatmapInfo.Ruleset = ruleset;
			beatmap.Difficulty.CircleSize = 12;
			beatmap.Difficulty.OverallDifficulty = 5;
			string section = "";
			Dictionary<int, ShredPracticeArticulation> articulations = new Dictionary<int, ShredPracticeArticulation>();
			bool format = false;
			bool correctMode = false;
			bool correctColumns = false;
			int lineNumber = 0;
			string line;
			while ((line = reader.ReadLine()) != null)
			{
				lineNumber++;
				line = line.Trim().TrimStart('\uFEFF');
				if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;
				if (line.StartsWith("osu file format v", StringComparison.Ordinal)) { format = true; continue; }
				if (line.StartsWith("[", StringComparison.Ordinal)) { section = line; continue; }
				try
				{
					if (section == "[HitObjects]")
					{
						string[] values = line.Split(',');
						if (values.Length < 5) throw new FormatException("Incomplete hit object.");
						int x = int.Parse(values[0], CultureInfo.InvariantCulture);
						int type = int.Parse(values[3], CultureInfo.InvariantCulture);
						if ((type & 1) == 0 || (type & (2 | 8 | 128)) != 0) throw new FormatException("This version accepts circles only, not sliders, spinners or holds.");
						if (x < 0 || x > 512) throw new FormatException("x must be within 0..512.");
						int column = Math.Min(11, x * 12 / 512);
						double time = double.Parse(values[2], CultureInfo.InvariantCulture);
						if (!double.IsFinite(time) || time < 0) throw new FormatException("Invalid time.");
						beatmap.HitObjects.Add(new ShredHitObject { StartTime = time, Row = column / 6, Fret = column % 6 });
						continue;
					}
					if (section == "[ShredArticulations]")
					{
						string[] values = line.Split(',');
						if (values.Length != 2) throw new FormatException("Use objectIndex,H or objectIndex,P.");
						int index = int.Parse(values[0], CultureInfo.InvariantCulture);
						ShredPracticeArticulation articulation;
						if ((values[1].Trim() == "H" || values[1].Trim() == "P" || values[1].Trim() == "HP")) articulation = ShredPracticeArticulation.HammerPull;
						else throw new FormatException("Unknown articulation.");
						articulations.Add(index, articulation);
						continue;
					}
					int colon = line.IndexOf(':');
					if (colon < 0) continue;
					string key = line.Substring(0, colon).Trim();
					string value = line.Substring(colon + 1).Trim();
					if (section == "[General]")
					{
						if (key == "Mode") correctMode = value == "3";
						if (key == "AudioFilename") beatmap.Metadata.AudioFile = value;
					}
					if (section == "[Metadata]")
					{
						if (key == "Title") beatmap.Metadata.Title = value;
						if (key == "Artist") beatmap.Metadata.Artist = value;
						if (key == "Version") beatmap.BeatmapInfo.DifficultyName = value;
					}
					if (section == "[Difficulty]")
					{
						if (key == "CircleSize") correctColumns = double.Parse(value, CultureInfo.InvariantCulture) == 12;
						if (key == "OverallDifficulty") beatmap.Difficulty.OverallDifficulty = float.Parse(value, CultureInfo.InvariantCulture);
					}
				}
				catch (Exception e) when (e is FormatException || e is OverflowException || e is ArgumentException)
				{
					throw new FormatException($"Line {lineNumber}: {e.Message}", e);
				}
			}
			if (!format || !correctMode || !correctColumns) throw new FormatException("Expected .osu, Mode:3, CircleSize:12.");
			foreach (KeyValuePair<int, ShredPracticeArticulation> entry in articulations)
			{
				if (entry.Key < 0 || entry.Key >= beatmap.HitObjects.Count) throw new FormatException("Articulation index outside map.");
				if (beatmap.HitObjects[entry.Key].Fret == 0 && entry.Value != ShredPracticeArticulation.Pick) throw new FormatException("Open hammer-on is not playable.");
				beatmap.HitObjects[entry.Key].Articulation = entry.Value;
			}
			beatmap.HitObjects.Sort((a,b) => a.StartTime.CompareTo(b.StartTime));
			return beatmap;
		}
	}
}
