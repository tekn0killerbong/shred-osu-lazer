using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred.Scoring;
using osu.Game.Rulesets.Shred.Gameplay;
using System.Threading;
namespace osu.Game.Rulesets.Shred.Objects
{
	public class ShredJudgement : Judgement
	{
		public override HitResult MaxResult => HitResult.Great;
	}
	public class ShredHitObject : HitObject, IHasXPosition, IHasColumn, IHasDuration
	{
		public int Column => Row * 6 + Fret;
		public float X
		{
			get => Column;
			set
			{
				int column = System.Math.Clamp((int)System.Math.Round(value), 0, 11);
				Row = column / 6;
				Fret = column % 6;
			}
		}
		public int Row { get; set; }
		public int Fret { get; set; }
		public ShredPracticeArticulation Articulation { get; set; }
		public double Duration { get; set; }
		public double EndTime => StartTime + Duration;
		public bool IsLong => Duration > 0;
		public bool IsWham => Articulation == ShredPracticeArticulation.Wham;
		protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
		{
			base.CreateNestedHitObjects(cancellationToken);
			if (IsLong) AddNested(new ShredHoldHead { StartTime = StartTime, Samples = Samples, Parent = this });
		}
		public override Judgement CreateJudgement() => new ShredJudgement();
		protected override HitWindows CreateHitWindows() => new ShredHitWindows();
	}
	public class ShredHoldHead : HitObject
	{
		public ShredHitObject Parent { get; set; }
		public override Judgement CreateJudgement() => new ShredJudgement();
		protected override HitWindows CreateHitWindows() => new ShredHitWindows();
	}
}
