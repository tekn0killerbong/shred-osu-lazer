using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred.Scoring;
using osu.Game.Rulesets.Shred.Gameplay;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
namespace osu.Game.Rulesets.Shred.Objects
{
	public class ShredJudgement : Judgement
	{
		public override HitResult MaxResult => HitResult.Great;
	}
	public class ShredHitObject : HitObject, IHasXPosition, IHasColumn, IHasDuration
#if SHRED_OFFICIAL
        , IHasPosition
#else
        , IHasTimingDistribution
#endif
	{
#if SHRED_OFFICIAL
        // Explicit carrier geometry leaves the editor's column-valued X intact.
        osuTK.Vector2 IHasPosition.Position
        {
            get => new osuTK.Vector2((Column + 0.5f) * 512 / 12, 192);
            set { X = System.Math.Clamp((int)(value.X * 12 / 512), 0, 11); }
        }
        public float Y { get => 192; set { } }
#endif
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
		// Editor provenance, independent for the head and tail of a sustain.
		public int HeadSnapDivisor { get; set; }
		public int TailSnapDivisor { get; set; }
		// Native clipboard serialisation carries this snapshot across maps/BPMs.
		// Runtime/editor processing replaces it; legacy map encoders omit it.
		public Beatmaps.ShredRhythmStamp EditorRhythm { get; set; }
		public double EndTime => StartTime + Duration;
		public bool IsLong => Duration > 0;
        public bool IncludeInTimingDistribution => !IsLong;
		public bool IsWham => Articulation == ShredPracticeArticulation.Wham;
		protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
		{
			base.CreateNestedHitObjects(cancellationToken);
			if (IsLong) AddNested(new ShredHoldHead { StartTime = StartTime, Samples = Samples, Parent = this });
		}
		public override Judgement CreateJudgement() => new ShredJudgement();
        public ShredHitWindows TimingWindows { get; } = new ShredHitWindows();
        protected override void ApplyDefaultsToSelf(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
            TimingWindows.SetDifficulty(difficulty.OverallDifficulty);
            HitWindows = IsLong ? HitWindows.Empty : TimingWindows;
            base.ApplyDefaultsToSelf(controlPointInfo, difficulty);
        }
        protected override HitWindows CreateHitWindows() => IsLong ? HitWindows.Empty : TimingWindows;
	}
	public class ShredHoldHead : HitObject
	{
		public ShredHitObject Parent { get; set; }
		public override Judgement CreateJudgement() => new ShredJudgement();
		protected override HitWindows CreateHitWindows() => new ShredHitWindows();
	}
}
