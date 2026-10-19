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
        protected override double InitialLifetimeOffset => this.field?.MaximumVisibleLeadTime ?? 600000;

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

        public bool TryHit(double? pickTime = null)
        {
            if (HeadJudged) return false;
            double offset = (pickTime ?? Time.Current) - HitObject.StartTime;
            if (HitObject.IsLong) return head?.TryHit(offset) == true;
            HitResult result = HitObject.HitWindows.ResultFor(offset);
            if (!result.IsHit()) return false;
            ApplyResult((judgement, saved) =>
            {
                judgement.Type = saved.result;
                ((ShredJudgementResult)judgement).PickOffset = saved.offset;
            }, (result, offset));
            return true;
        }
        public void CheckExpiry() { head?.CheckExpiry(); UpdateResult(false); }
        public void MissHead()
        {
            if (HeadJudged) return;
            if (HitObject.IsLong) head?.Miss();
            else ApplyMinResult();
        }
        public void Release()
        {
            if (!IsHolding) return;
            double offset = Time.Current - HitObject.EndTime;
            HitResult result = HitResult.Great;
            if (offset < 0)
                result = ((ShredHitWindows)HitObject.HitWindows).ResultForEarlyRelease(offset);
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
                HitResult result = HitObject.HitWindows.ResultFor(timeOffset);
                if (result.IsHit()) ApplyResult(result);
            }
            else if (timeOffset > HitObject.HitWindows.WindowFor(HitResult.Miss)) ApplyMinResult();
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
                if (Hidden) Alpha = (float)Math.Clamp(((HitObject.StartTime - Time.Current) / ShredPlayfield.ApproachTime - 0.3) / 0.3, 0, 1);
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

        public bool TryHit(double offset)
        {
            if (Judged) return false;
            HitResult result = HitObject.HitWindows.ResultFor(offset);
            if (!result.IsHit()) return false;
            ApplyResult((judgement, saved) =>
            {
                judgement.Type = saved.result;
                ((ShredJudgementResult)judgement).PickOffset = saved.offset;
            }, (result, offset));
            return true;
        }
        public void CheckExpiry() => UpdateResult(false);
        public void Miss() { if (!Judged) ApplyMinResult(); }
        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            // Animate the played note at onset as well as at hold completion.
            if (state == ArmedState.Hit)
            {
                if (HitObject.Parent.IsWham) field.ShowWhamSuccess();
                else field.ShowSuccess(HitObject.Parent);
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
            else if (timeOffset > HitObject.HitWindows.WindowFor(HitResult.Miss)) ApplyMinResult();
        }
    }

    public static class ShredInputJudgement
    {
        public static bool IsTooEarlyToTarget(HitWindows windows, double offset)
            => offset < -windows.WindowFor(HitResult.Miss);
        public static bool IsWrongPickInWindow(HitWindows windows, double offset)
            => Math.Abs(offset) <= windows.WindowFor(HitResult.Miss);
    }
}
