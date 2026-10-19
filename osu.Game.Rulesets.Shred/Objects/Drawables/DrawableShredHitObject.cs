using System;
using System.Collections.Generic;
using osu.Game.Audio;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred.UI;
using osu.Game.Rulesets.Shred.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Shred.Objects.Drawables
{
    public partial class DrawableShredHitObject : DrawableHitObject<ShredHitObject>
    {
        [Resolved] private ShredPlayfield field { get; set; }
        private ShredNoteVisual visual;
        private readonly Container nested = new Container { RelativeSizeAxes = Axes.Both };
        private DrawableShredHoldHead head;
        public bool Hidden { get; set; }
        private bool frozen;
        private Vector2 frozenPosition;
        public bool HeadJudged => HitObject.IsLong ? head?.Judged == true : Judged;
        public bool HeadHit => HitObject.IsLong ? head?.Result?.IsHit == true : Result?.IsHit == true;
        public bool IsHolding => HitObject.IsLong && HeadHit && !Judged;
        public double HeadOffset => Time.Current - HitObject.StartTime;
		protected override double InitialLifetimeOffset
		{
			get
			{
				if (this.field == null) return 600000;
				if (HitObject.IsWham) return this.field.MaximumWhamVisibleLeadTime;
				return this.field.MaximumVisibleLeadTime;
			}
		}

        public DrawableShredHitObject(ShredHitObject hitObject) : base(hitObject)
        {
            Origin = Anchor.Centre;
            AddInternal(nested);
        }

        [BackgroundDependencyLoader]
        private void load() => AddInternal(visual = new ShredNoteVisual(HitObject, field.Skin, field.Assets));

        public override IEnumerable<HitSampleInfo> GetSamples()
        {
            // The nested head keeps the attack samples. Sustain completion is silent.
            if (HitObject.IsLong) return Array.Empty<HitSampleInfo>();
            return base.GetSamples();
        }

        protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject)
            => head = new DrawableShredHoldHead((ShredHoldHead)hitObject);
        protected override void AddNestedHitObject(DrawableHitObject hitObject) => nested.Add(hitObject);
        protected override void ClearNestedHitObjects() { nested.Clear(); head = null; }

        protected override JudgementResult CreateResult(Judgement judgement)
            => new ShredJudgementResult(HitObject, judgement);

        public bool TryHit(double? pickTime = null, bool picked = false)
        {
            if (HeadJudged) return false;
            if (!HitObject.IsWham && field != null)
                return field.RequestChordHit(this, pickTime ?? Time.Current, picked);
            return CommitHead(pickTime ?? Time.Current, picked);
        }
        public bool CommitHead(double pickTime, bool picked)
        {
            if (HeadJudged) return false;
            double offset = pickTime - HitObject.StartTime;
            if (HitObject.IsLong) return head?.TryHit(offset, picked && HitObject.Articulation == Gameplay.ShredPracticeArticulation.HammerPull) == true;
            HitResult result = HitObject.TimingWindows.ResultFor(offset);
            if (!result.IsHit()) return false;
            if (picked && HitObject.Articulation == Gameplay.ShredPracticeArticulation.HammerPull) result = HitResult.Good;
            ApplyResult((judgement, saved) =>
            {
                judgement.Type = saved.result;
                ((ShredJudgementResult)judgement).PickOffset = saved.offset;
            }, (result, offset));
            return true;
        }
        public void CheckExpiry() { head?.CheckExpiry(); UpdateResult(false); }
        public void MissHead(double? inputTime = null)
        {
            if (HeadJudged) return;
            double offset = (inputTime ?? Time.Current) - HitObject.StartTime;
            if (HitObject.IsLong) head?.Miss(offset);
            else ApplyResult((result, savedOffset) =>
            {
                result.Type = HitResult.Miss;
                ((ShredJudgementResult)result).PickOffset = savedOffset;
            }, offset);
        }
        public void Release()
        {
            if (!IsHolding) return;
            double offset = Time.Current - HitObject.EndTime;
            HitResult result = HitResult.Great;
            if (offset < 0)
                result = ((ShredHitWindows)HitObject.TimingWindows).ResultForEarlyRelease(offset);
            if (!result.IsHit()) result = HitResult.Miss;
            ApplyResult(result);
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (HitObject.IsLong)
            {
                if (Time.Current >= HitObject.EndTime)
                {
                    if (HeadHit) ApplyResult(HitResult.Great);
                    else if (head?.Judged == true) ApplyMinResult();
                }
                return;
            }
            if (userTriggered)
            {
                HitResult result = HitObject.TimingWindows.ResultFor(timeOffset);
                if (result.IsHit()) ApplyResult(result);
            }
            else if (timeOffset >= HitObject.TimingWindows.WindowFor(HitResult.Miss)
                && field?.HasPendingPick(this) != true)
            {
                if (field != null && !HitObject.IsWham) field.ExpireChord(this);
                else ApplyMinResult();
            }
        }

        protected override void Update()
        {
            base.Update();
            bool failed = (HeadJudged && !HeadHit) || (Judged && Result?.IsHit != true);
            double visibleStart = IsHolding ? Math.Min(Time.Current, HitObject.EndTime) : HitObject.StartTime;
            Size = HitObject.IsWham ? field.WhamSize : field.NoteSize(HitObject.Fret);
            Position = frozen ? frozenPosition : field.PositionAt(HitObject, visibleStart);
            // The playfield clips at its actual edges. Do not hide notes at
            // the old, short ScrollLength horizon while they are on screen.
            if (!Judged)
            {
                Alpha = 1;
                if (Hidden) Alpha = (float)Math.Clamp(((HitObject.StartTime - Time.Current) / field.MapApproachTime - 0.3) / 0.3, 0, 1);
            }
            visual?.Layout(field.PositionAt(HitObject, HitObject.EndTime) - Position, field.Unit, IsHolding, failed);
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            if (state == ArmedState.Idle) { frozen = false; return; }
            if (state == ArmedState.Hit)
            {
                if (HitObject.IsWham) field.ShowWhamSuccess();
                else field.ShowSuccess(HitObject);
                frozenPosition = field.PositionAt(HitObject, HitObject.IsLong ? HitObject.EndTime : HitObject.StartTime);
                frozen = true;
                this.FadeOut(35).Expire();
            }
            else
            {
                // Keep a failed sustain visible until its tail reaches judgement.
                double delay = HitObject.IsLong ? Math.Max(0, HitObject.EndTime - Time.Current) : 0;
                this.Delay(delay).FadeOut(field.Skin.MissFadeMs).Expire();
            }
        }
    }

    public partial class DrawableShredHoldHead : DrawableHitObject<ShredHoldHead>
    {
        [Resolved] private ShredPlayfield field { get; set; }
        protected override double InitialLifetimeOffset => 11785;
        public DrawableShredHoldHead(ShredHoldHead note) : base(note) { AlwaysPresent = true; }
        protected override JudgementResult CreateResult(Judgement judgement)
            => new ShredJudgementResult(HitObject, judgement);

        public bool TryHit(double offset, bool pickedHammerPull = false)
        {
            if (Judged) return false;
            HitResult result = HitObject.HitWindows.ResultFor(offset);
            if (!result.IsHit()) return false;
            if (pickedHammerPull) result = HitResult.Good;
            ApplyResult((judgement, saved) =>
            {
                judgement.Type = saved.result;
                ((ShredJudgementResult)judgement).PickOffset = saved.offset;
            }, (result, offset));
            return true;
        }
        public void CheckExpiry() => UpdateResult(false);
        public void Miss(double? offset = null)
        {
            if (Judged) return;
            ApplyResult((result, savedOffset) =>
            {
                result.Type = HitResult.Miss;
                ((ShredJudgementResult)result).PickOffset = savedOffset;
            }, offset ?? Time.Current - HitObject.StartTime);
        }
        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            // Animate the played note at onset as well as at hold completion.
            if (state == ArmedState.Hit)
            {
                if (HitObject.Parent.IsWham) field.ShowWhamSuccess();
                else field.ShowSuccess(HitObject.Parent, completion: false);
            }
            // Keep the onset result available for the parent sustain until its
            // release has resolved. The head itself has no visual animation.
            LifetimeEnd = Math.Max(HitObject.Parent.EndTime, HitObject.StartTime + HitObject.HitWindows.WindowFor(HitResult.Miss)) + 500;
        }
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (userTriggered)
            {
                HitResult result = HitObject.HitWindows.ResultFor(timeOffset);
                if (result.IsHit()) ApplyResult(result);
            }
            else if (timeOffset >= HitObject.HitWindows.WindowFor(HitResult.Miss)
                && field?.HasPendingPick(HitObject.Parent) != true)
            {
                if (field != null && !HitObject.Parent.IsWham) field.ExpireChord(HitObject.Parent);
                else ApplyMinResult();
            }
        }
    }

    public static class ShredInputJudgement
    {
        public static bool IsTooEarlyToTarget(HitWindows windows, double offset)
            => offset < -windows.WindowFor(HitResult.Miss);
        public static bool IsWrongPickInWindow(HitWindows windows, double offset)
            => offset >= -windows.WindowFor(HitResult.Miss) && offset <= windows.WindowFor(HitResult.Miss);
    }
}
