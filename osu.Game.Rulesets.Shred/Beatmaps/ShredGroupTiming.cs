using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Shred.Objects;

namespace osu.Game.Rulesets.Shred.Beatmaps
{
	// Clipboard-only musical coordinates. The map encoders deliberately omit
	// these values; a reopened editor derives them from its current timing.
	public sealed class ShredRhythmStamp
	{
		public double Start { get; set; }
		public double End { get; set; }
		public double HeadBeat { get; set; }
		public double TailBeat { get; set; }
		public int HeadDivisor { get; set; }
		public int TailDivisor { get; set; }
		[JsonIgnore]
		public bool Valid => double.IsFinite(Start) && double.IsFinite(End)
			&& double.IsFinite(HeadBeat) && double.IsFinite(TailBeat)
			&& End >= Start && TailBeat >= HeadBeat;
	}

	// Continuous beat coordinates integrate every crossed BPM section. Grid
	// origins can reset at timing points; elapsed musical intervals do not.
	public sealed class ShredBeatTimeline
	{
		private readonly double[] times;
		private readonly double[] lengths;
		private readonly double[] beats;
		private readonly ControlPointInfo grid = new ControlPointInfo();
		public ShredBeatTimeline(ControlPointInfo timing)
		{
			TimingControlPoint[] points = timing.TimingPoints.ToArray();
			if (points.Length == 0) points = new[] { timing.TimingPointAt(0) };
			times = points.Select(p => p.Time).ToArray();
			lengths = points.Select(p => p.BeatLength).ToArray();
			foreach (var point in points)
				grid.Add(point.Time, new TimingControlPoint { BeatLength = point.BeatLength });
			beats = new double[points.Length];
			beats[0] = times[0] / lengths[0];
			for (int i = 1; i < beats.Length; i++)
				beats[i] = beats[i - 1] + (times[i] - times[i - 1]) / lengths[i - 1];
		}
		public bool SameTiming(ShredBeatTimeline other) => other != null
			&& times.SequenceEqual(other.times) && lengths.SequenceEqual(other.lengths);
		private static int section(double[] values, double value)
		{
			int index = Array.BinarySearch(values, value);
			if (index < 0) index = ~index - 1;
			return Math.Max(0, index);
		}
		public double BeatAt(double time)
		{
			int index = section(times, time);
			return beats[index] + (time - times[index]) / lengths[index];
		}
		public double TimeAt(double beat)
		{
			int index = section(beats, beat);
			return ShredGridTiming.Precise(times[index] + (beat - beats[index]) * lengths[index]);
		}
		public double Snap(double time, int divisor) => ShredGridTiming.Snap(grid, divisor, time);
		public int DivisorAt(double time, int preferred = 0)
		{
			if (ShredObjectSnap.Valid(preferred) && Math.Abs(Snap(time, preferred) - time) < .000002) return preferred;
			for (int d = 1; d <= Screens.Edit.BindableBeatDivisor.MAXIMUM_DIVISOR; d++)
				if (Math.Abs(Snap(time, d) - time) < .000002) return d;
			// Recover a rounded legacy position only when its own known grid is
			// nearby. Do not force a deliberate off-grid position onto a coarse beat.
			if (ShredObjectSnap.Valid(preferred) && Math.Abs(Snap(time, preferred) - time) < 1) return preferred;
			return 0;
		}
		private double captureBeat(double time, int divisor)
		{
			if (!ShredObjectSnap.Valid(divisor)) return BeatAt(time);
			time = Snap(time, divisor);
			int index = section(times, time);
			// Recover the exact subdivision before subtracting/multiplying BPMs.
			// Carrying fixed-point rounding errors as beats can move a tail by a
			// quantum on every copy even though both source ends were exact ticks.
			return beats[index] + Math.Round((time - times[index]) / lengths[index] * divisor) / divisor;
		}
		public ShredRhythmStamp Capture(double start, double end, int headDivisor = 0, int tailDivisor = 0)
		{
			headDivisor = DivisorAt(start, headDivisor);
			tailDivisor = DivisorAt(end, tailDivisor);
			return new ShredRhythmStamp {
				Start = start, End = end, HeadBeat = captureBeat(start, headDivisor), TailBeat = captureBeat(end, tailDivisor),
				HeadDivisor = headDivisor, TailDivisor = tailDivisor
			};
		}
		public ShredRhythmStamp Capture(ShredHitObject note) => Capture(note.StartTime, note.EndTime, note.HeadSnapDivisor, note.TailSnapDivisor);
	}

	public static class ShredGroupTiming
	{
		private static int commonDivisor(int a, int b)
		{
			if (!ShredObjectSnap.Valid(a) || !ShredObjectSnap.Valid(b)) return 0;
			int x = a, y = b;
			while (y != 0) { int remainder = x % y; x = y; y = remainder; }
			int result = a / x * b;
			if (result > ShredObjectSnap.MaximumProvenanceDivisor) return 0;
			return result;
		}
		// One shared anchor remains free to follow the current spacing. The
		// common source/anchor subdivision then uses the actual local grid origin,
		// including timing-point resets, without reintroducing the old 1/1 lock.
		public static bool Apply(IEnumerable<(ShredHitObject note, ShredRhythmStamp source)> source,
			double targetTime, ShredBeatTimeline destination)
		{
			var entries = source.ToArray();
			if (entries.Length == 0 || !double.IsFinite(targetTime) || targetTime < 0
				|| entries.Any(e => e.source == null || !e.source.Valid)) return false;
			var anchor = entries.MinBy(e => e.source.Start).source;
			double targetBeat = destination.BeatAt(targetTime);
			var endpoints = entries.SelectMany(e => new[] {
				(time: e.source.Start, beat: e.source.HeadBeat, divisor: e.source.HeadDivisor),
				(time: e.source.End, beat: e.source.TailBeat, divisor: e.source.TailDivisor)
			}).ToArray();
			// Use one shared lattice, not one rounding resolution per note: an
			// offset reset must not move binary and triplet members differently.
			int groupDivisor = endpoints.Aggregate(destination.DivisorAt(targetTime), (d, p) => commonDivisor(d, p.divisor));
			var points = endpoints.GroupBy(p => p.time).ToDictionary(g => g.Key, g =>
			{
				int divisor = groupDivisor;
				double time = destination.TimeAt(targetBeat + (g.First().beat - anchor.HeadBeat));
				if (divisor != 0) time = destination.Snap(time, divisor);
				return (time, divisor);
			});
			var moved = entries.Select(e => (
				e.note,
				start: points[e.source.Start].time,
				end: points[e.source.End].time,
				headDivisor: points[e.source.Start].divisor,
				tailDivisor: points[e.source.End].divisor)).ToArray();
			if (moved.Any(e => !double.IsFinite(e.start) || !double.IsFinite(e.end) || e.start < 0 || e.end < e.start)) return false;
			// A non-zero LN must never silently become a short note. If a tempo
			// reset would collapse it, leave this operation untransformed instead.
			if (moved.Any(e => e.note.IsLong && e.end <= e.start)) return false;
			foreach (var entry in moved)
			{
				entry.note.StartTime = entry.start;
				entry.note.Duration = entry.end - entry.start;
				if (entry.headDivisor != 0) entry.note.HeadSnapDivisor = entry.headDivisor;
				if (entry.tailDivisor != 0) entry.note.TailSnapDivisor = entry.tailDivisor;
			}
			return true;
		}
	}
}
