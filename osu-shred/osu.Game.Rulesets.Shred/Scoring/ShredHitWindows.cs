using System;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Shred.Scoring
{
	public class ShredHitWindows : HitWindows
	{
		private double difficulty;
		public double SpeedMultiplier { get; set; } = 1;
		public double DifficultyMultiplier { get; set; } = 1;
		public override void SetDifficulty(double value) => difficulty = Math.Clamp(value, 0, 10);
		public override bool IsHitResultAllowed(HitResult result)
			=> result == HitResult.Great || result == HitResult.Good || result == HitResult.Miss;
		public override double WindowFor(HitResult result)
		{
			// HitWindows exposes symmetric magnitudes. Miss is the EARLY outer
			// bound only; drawable expiry and late targeting use Great.
			if (!IsHitResultAllowed(result)) throw new ArgumentOutOfRangeException(nameof(result));
			double window = 500 - 80 * difficulty;
			if (difficulty > 5) window = 100 - 13.1 * (difficulty - 5);
			return window * SpeedMultiplier / DifficultyMultiplier
                + (result == HitResult.Miss ? 150 * SpeedMultiplier : 0);
		}
		public HitResult ResultForEarlyRelease(double offset)
		{
			if (Math.Abs(offset) <= 350 * SpeedMultiplier) return HitResult.Great;
			return HitResult.Miss;
		}
	}
}
