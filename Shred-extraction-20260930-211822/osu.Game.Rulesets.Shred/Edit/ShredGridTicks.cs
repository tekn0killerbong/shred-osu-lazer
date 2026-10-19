using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Shred.Edit
{
	public readonly struct ShredGridTick
	{
		public readonly double Time;
		public readonly int Divisor;
		public ShredGridTick(double time, int divisor)
		{
			Time = time;
			Divisor = divisor;
		}
	}

	// Frets and whammy share the same timing origin, subdivisions and colours.
	public static class ShredGridTicks
	{
		public static double Next(ControlPointInfo controlPoints, int divisor, double position)
		{
			if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor));
			double span = controlPoints.TimingPointAt(position).BeatLength / divisor;
			foreach (ShredGridTick tick in Enumerate(controlPoints, divisor, position + 0.001, position + span + 1))
				if (tick.Time > position + 0.000001) return tick.Time;
			return position + span;
		}

		public static IEnumerable<ShredGridTick> Enumerate(ControlPointInfo controlPoints, int divisor, double start, double end)
		{
			if (divisor <= 0 || end < start) yield break;
			List<TimingControlPoint> segments = new List<TimingControlPoint> { controlPoints.TimingPointAt(start) };
			segments.AddRange(controlPoints.TimingPoints.Where(point => point.Time > start && point.Time <= end));
			for (int segment = 0; segment < segments.Count; segment++)
			{
				TimingControlPoint timing = segments[segment];
				double step = timing.BeatLength / divisor;
				if (!double.IsFinite(step) || step <= 0) continue;
				double nextTime = double.PositiveInfinity;
				if (segment + 1 < segments.Count) nextTime = segments[segment + 1].Time;
				int beat = Math.Max(0, (int)Math.Ceiling((start - timing.Time) / step - 0.000001));
				for (double time = timing.Time + beat * step; time <= end + 0.000001 && time < nextTime; time = timing.Time + ++beat * step)
				{
					if (time < 0) continue;
					yield return new ShredGridTick(time, BindableBeatDivisor.GetDivisorForBeatIndex(beat, divisor));
				}
			}
		}
	}
}
