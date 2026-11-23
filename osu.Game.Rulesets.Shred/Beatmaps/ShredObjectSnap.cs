using System;
using System.Linq;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Shred.Beatmaps
{
	public static class ShredObjectSnap
	{
		// A mixed pattern shifted by another subdivision can require a compound
		// denominator (e.g. 1/64 + 1/6 uses 1/192). This is provenance, not a new
		// selectable editor spacing; the UI still has its normal 1..64 range.
		public const int MaximumProvenanceDivisor = 4096;
		public static bool Valid(int divisor) => divisor >= 1 && divisor <= MaximumProvenanceDivisor;
		public static bool HasGrid(ShredHitObject note) => Valid(note.HeadSnapDivisor) && (!note.IsLong || Valid(note.TailSnapDivisor));

		public static void Initialise(ShredHitObject note, ControlPointInfo timing)
		{
			if (!Valid(note.HeadSnapDivisor)) note.HeadSnapDivisor = Infer(timing, note.StartTime);
			if (note.IsLong && !Valid(note.TailSnapDivisor)) note.TailSnapDivisor = Infer(timing, note.EndTime);
		}

		public static int Infer(ControlPointInfo timing, double time)
		{
			// Prefer the coarsest exact subdivision. Shared beat lines have no
			// recoverable binary/triplet intent in files predating this metadata.
			foreach (int divisor in Enumerable.Range(1, BindableBeatDivisor.MAXIMUM_DIVISOR))
				if (Math.Abs(ShredGridTiming.Snap(timing, divisor, time) - time) < 0.001) return divisor;
			// Off-grid historical notes: best available estimate, never the
			// editor's currently displayed subdivision.
			return timing.GetClosestBeatDivisor(time);
		}

		public static bool Apply(ShredHitObject note, ControlPointInfo timing)
		{
			Initialise(note, timing);
			double start = ShredGridTiming.Snap(timing, note.HeadSnapDivisor, note.StartTime);
			double duration = 0;
			if (note.IsLong)
			{
				double end = ShredGridTiming.Snap(timing, note.TailSnapDivisor, note.EndTime);
				// A short sustain must not disappear when both ends round to one line.
				if (end <= start)
				{
					double step = timing.TimingPointAt(start).BeatLength / note.TailSnapDivisor;
					end = ShredGridTiming.Snap(timing, note.TailSnapDivisor, start + step);
				}
				duration = Math.Max(0, end - start);
			}
			bool changed = note.StartTime != start || note.Duration != duration;
			note.StartTime = start;
			note.Duration = duration;
			return changed;
		}
	}
}
