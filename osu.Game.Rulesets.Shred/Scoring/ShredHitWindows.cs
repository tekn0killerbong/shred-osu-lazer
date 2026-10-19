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
            if (!IsHitResultAllowed(result)) throw new ArgumentOutOfRangeException(nameof(result));
            double window = 120 - 9 * difficulty;
            if (result == HitResult.Miss) window = 180 - 3 * difficulty;
            return window * SpeedMultiplier / DifficultyMultiplier;
		}
		public HitResult ResultForEarlyRelease(double offset)
		{
			if (Math.Abs(offset) <= 350 * SpeedMultiplier) return HitResult.Great;
			return HitResult.Miss;
		}
	}
}
