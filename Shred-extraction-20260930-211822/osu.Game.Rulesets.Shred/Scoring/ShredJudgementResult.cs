using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Shred.Scoring
{
	public class ShredJudgementResult : JudgementResult
	{
		// Lazer reserves TimeOffset's setter for the engine. Keep the original
		// pick independently: result application can occur after fret correction.
		public double? PickOffset { get; set; }
		public ShredJudgementResult(HitObject hitObject, Judgement judgement) : base(hitObject, judgement) { }
	}
}
