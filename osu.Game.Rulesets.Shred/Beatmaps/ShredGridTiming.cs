using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Shred.Beatmaps
{
	public static class ShredGridTiming
	{
		// A binary fraction of a millisecond keeps start + duration exactly equal
		// to a shared endpoint. Its error is below one nanosecond; whole-ms
		// rounding would distort triplets and most BPMs.
		private const double units_per_ms = 1048576;
		private static readonly int[] customDivisors = Enumerable.Range(1, BindableBeatDivisor.MAXIMUM_DIVISOR)
			.Except(BindableBeatDivisor.PREDEFINED_DIVISORS).ToArray();
		public static double Precise(double time) => Math.Round(time * units_per_ms) / units_per_ms;

		public static double Snap(ControlPointInfo timing, int divisor, double time)
			=> Precise(timing.GetClosestSnappedTime(time, divisor));

		public static double Repair(ControlPointInfo timing, double time) => repair(timing, time, out _);
		private static double repair(ControlPointInfo timing, double time, out double distance)
		{
			// Repair historical sub-ms offsets, never snap a whole selection to
			// the currently displayed subdivision. Common and triplet grids coexist.
			double nearest = time;
			distance = double.PositiveInfinity;
			foreach (int divisor in BindableBeatDivisor.PREDEFINED_DIVISORS)
			{
				double candidate = Snap(timing, divisor, time);
				double error = Math.Abs(candidate - time);
				if (error >= 1 || error >= distance) continue;
				nearest = candidate;
				distance = error;
				if (distance == 0) return candidate;
			}
			// Exact positions on custom spacings (for example 1/32 or 1/64)
			// must not migrate to a nearby common/triplet line during a reload.
			double precise = Precise(time);
			foreach (int divisor in customDivisors)
			{
				double candidate = Snap(timing, divisor, time);
				if (candidate != precise) continue;
				distance = Math.Abs(candidate - time);
				return candidate;
			}
			return Precise(nearest);
		}

		public static void Repair(IEnumerable<ShredHitObject> source, ControlPointInfo timing)
		{
			var notes = source.OrderBy(n => n.StartTime).ToArray();
			if (notes.Length == 0) return;
			var starts = new double[notes.Length];
			var ends = new double[notes.Length];
			var points = new List<(double time, int index, bool tail)>();
			for (int i = 0; i < notes.Length; i++)
			{
				starts[i] = notes[i].StartTime;
				ends[i] = notes[i].EndTime;
				points.Add((starts[i], i, false));
				if (notes[i].Duration > 0) points.Add((ends[i], i, true));
			}
			// An LN endpoint and a head sharing the same step use the very same
			// value, even when they came from different clipboard calculations.
			var ordered = points.OrderBy(p => p.time).ToArray();
			for (int first = 0; first < ordered.Length;)
			{
				int next = first + 1;
				while (next < ordered.Length && ordered[next].time - ordered[first].time < 1) next++;
				// A precisely aligned endpoint is a better reference than another
				// member's rounded timestamp, especially near neighbouring subdivisions.
				double time = repair(timing, ordered[first].time, out double bestError);
				for (int i = first + 1; i < next; i++)
				{
					double candidate = repair(timing, ordered[i].time, out double error);
					if (error >= bestError) continue;
					time = candidate;
					bestError = error;
				}
				for (int i = first; i < next; i++)
				{
					var point = ordered[i];
					if (point.tail) ends[point.index] = time;
					else starts[point.index] = time;
				}
				first = next;
			}
			for (int i = 0; i < notes.Length; i++)
			{
				bool sustain = notes[i].Duration > 0;
				notes[i].StartTime = starts[i];
				notes[i].Duration = 0;
				if (sustain) notes[i].Duration = Math.Max(0, ends[i] - starts[i]);
			}
		}
	}
}
