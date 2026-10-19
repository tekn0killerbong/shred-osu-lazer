using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Gameplay;

namespace osu.Game.Rulesets.Shred.Mods
{
	public class ShredModGuitarHero : Mod, IApplicableToBeatmap
	{
		public override string Name => "Guitar Hero";
		public override string Acronym => "GH";
		public override LocalisableString Description => "One fret row, one strum bar. Use row 2 frets and either row of picks.";
		public override ModType Type => ModType.Conversion;
		public override IconUsage? Icon => new IconUsage('\uE001', "ShredMods");
		public override bool Ranked => false;
		// A vertical mirror would move the sole playable row away from its inputs.
		public override Type[] IncompatibleMods => new[] { typeof(ShredModMirror) };
		public void ApplyToBeatmap(IBeatmap beatmap)
		{
			var occupied = new List<ShredHitObject>();
			var lastSource = new Dictionary<int, int>();
			var lastTarget = new Dictionary<int, int>();
			var remove = new List<ShredHitObject>();
			foreach (ShredHitObject note in beatmap.HitObjects.OfType<ShredHitObject>().OrderBy(n => n.StartTime).ThenBy(n => n.Row))
			{
				int sourceRow = note.Row;
				int sourceFret = note.Fret;
				if (note.IsWham) { note.Articulation = ShredPracticeArticulation.Pick; sourceFret = 3; }
				note.Row = 1;
				note.Fret = sourceFret;
				occupied.RemoveAll(n => n.EndTime < note.StartTime - 0.5);
				if (sourceFret > 0)
				{
					bool alternates = lastSource.TryGetValue(sourceFret, out int previousRow) && previousRow != sourceRow;
					int preferred = sourceFret;
					if (alternates && lastTarget[sourceFret] == sourceFret)
					{
						preferred++;
						if (preferred > 5) preferred = 4;
					}
					int[] candidates = Enumerable.Range(1, 5).OrderBy(f => Math.Abs(f - preferred)).ThenBy(f => f).ToArray();
					int available = candidates.FirstOrDefault(f => !occupied.Any(n => n.Fret == f));
					if (available == 0)
					{
						// More than five simultaneous frets cannot fit: merge into the closest held fret.
						ShredHitObject retained = occupied.OrderBy(n => Math.Abs(n.Fret - preferred)).First();
						retained.Duration = Math.Max(retained.EndTime, note.EndTime) - retained.StartTime;
						remove.Add(note);
					}
					else note.Fret = available;
					lastSource[sourceFret] = sourceRow;
					lastTarget[sourceFret] = note.Fret;
				}
				if (!remove.Contains(note)) occupied.Add(note);
			}
			if (beatmap is Beatmap<ShredHitObject> shred)
				foreach (ShredHitObject note in remove) shred.HitObjects.Remove(note);
		}
	}
}
