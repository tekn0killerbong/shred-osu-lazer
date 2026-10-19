using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Game.Beatmaps;
using osu.Game.Audio;
using osu.Game.Screens.Edit;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.UI;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;
namespace osu.Game.Rulesets.Shred.Edit
{
	public partial class DrawableShredEditorRuleset : DrawableRuleset<ShredHitObject>
	{
		public DrawableShredEditorRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod> mods) : base(ruleset, beatmap, mods) { }
		// The native editor wrapper regenerates AutoPlay after each edit. This editor
        // renders unjudged notes directly and deliberately does not play replay inputs.
        // Passing these scores to the gameplay implementation would require a replay
        // input manager and throw during entry into the editor.
        public override void SetReplayScore(osu.Game.Scoring.Score replayScore)
        {
            ReplayScore = replayScore;
        }
        protected override Playfield CreatePlayfield() => new ShredEditorPlayfield();
		protected override PassThroughInputManager CreateInputManager() => new PassThroughInputManager();
		public override DrawableHitObject<ShredHitObject> CreateDrawableRepresentation(ShredHitObject note) => new DrawableShredEditorNote(note);
	}
    public partial class DrawableShredEditorNote : DrawableHitObject<ShredHitObject>
    {
#if SHRED_OFFICIAL
        public override IEnumerable<osu.Game.Audio.HitSampleInfo> GetSamples()
            => Compatibility.ShredEditorCodec.PlayableSamples(HitObject.Samples);
#endif
        [Resolved] private ShredEditorPlayfield field { get; set; }
        [Resolved] private EditorClock editorClock { get; set; }
        [Resolved(canBeNull: true)] private ISamplePlaybackDisabler playbackDisabler { get; set; }
        private double? previousSampleTime;
        private ShredNoteVisual visual;
        private readonly Container clip = new Container { Masking = true };
        public bool EditorSelected { get; set; }
        private readonly Container nested = new Container { RelativeSizeAxes = Axes.Both };
        protected override double InitialLifetimeOffset => double.MaxValue;
        public DrawableShredEditorNote(ShredHitObject note) : base(note) { Origin = Anchor.Centre; AlwaysPresent = true; AddInternal(nested); }
        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(clip);
            clip.Add(visual = new ShredNoteVisual(HitObject, field.Skin)
            {
                RelativeSizeAxes = Axes.None,
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre
            });
        }
        protected override DrawableHitObject CreateNestedHitObject(osu.Game.Rulesets.Objects.HitObject note)
            => new DrawableShredEditorHead((ShredHoldHead)note);
        protected override void AddNestedHitObject(DrawableHitObject note) => nested.Add(note);
        protected override void ClearNestedHitObjects() => nested.Clear();
        protected override void Update()
        {
            base.Update();
            // The editor intentionally has no gameplay replay/judgements. Audition
            // each head once during forward playback, using native sample routing.
            double sampleTime = editorClock.CurrentTime;
            if (editorClock.IsRunning && !editorClock.SeekingOrStopped.Value
                && playbackDisabler?.SamplePlaybackDisabled.Value != true
                && previousSampleTime.HasValue && sampleTime > previousSampleTime.Value
                && previousSampleTime.Value <= HitObject.StartTime && sampleTime > HitObject.StartTime)
                PlaySamples();
            // Updating even while paused/seeking prevents catch-up hitsounds.
            previousSampleTime = sampleTime;
            // Keep time/audio updates alive for seeking, but do not lay out or draw
            // the whole song each frame. Long sustains remain visible until their tail.
            if (HitObject.EndTime < field.CurrentTime - ShredChordRules.ChordTolerance || HitObject.StartTime > field.LastVisibleTime)
            {
                clip.Alpha = 0;
                return;
            }
            clip.Alpha = 1;
            int column = HitObject.IsWham ? 12 : HitObject.Column;
            // Keep the head at its real time. The receptor mask clips it as it passes,
            // leaving only the sustain body visible during subsequent scrolling.
            Position = field.PositionAt(column, HitObject.StartTime);
            // Whams have no separate head. Clamp their visible bar to the
            // slanted judgement edge rather than cutting it with a vertical mask.
            if (HitObject.IsWham) Position = field.PositionAt(column, Math.Max(HitObject.StartTime, field.CurrentTime));
            Size = HitObject.IsWham ? field.WhamSize : field.NoteSize(HitObject.Fret);
            // No downstream fragment survives once the object has passed.
            Alpha = HitObject.EndTime >= field.CurrentTime - ShredChordRules.ChordTolerance ? 1 : 0;
            Vector2 minimum = Vector2.Zero;
            Vector2 maximum = field.DrawSize;
            if (!HitObject.IsWham)
            {
                float receiverY = field.Receiver(HitObject.Row, HitObject.Fret).Y;
                float haloMargin = 0;
                if (HitObject.Articulation == ShredPracticeArticulation.HammerPull && HitObject.StartTime >= field.CurrentTime - ShredChordRules.ChordTolerance)
                    haloMargin = field.Skin.HaloRadius + 2;
                // Only an unpassed hammer-pull head may glow beyond the case.
                // As soon as its head passes, Layout hides the entire halo and
                // this mask returns to the normal sustain boundary.
                if (field.Skin.Direction(HitObject.Row).Y > 0) maximum.Y = receiverY + Size.Y / 2 + haloMargin;
                else minimum.Y = receiverY - Size.Y / 2 - haloMargin;
            }
            Vector2 clipMin = ToLocalSpace(field.ToScreenSpace(minimum));
            Vector2 clipMax = ToLocalSpace(field.ToScreenSpace(maximum));
            clip.Position = clipMin;
            clip.Size = new Vector2(Math.Max(0, clipMax.X - clipMin.X), Math.Max(0, clipMax.Y - clipMin.Y));
            visual.Size = Size;
            visual.Position = DrawSize / 2 - clipMin;
            visual.Layout(field.PositionAt(column, HitObject.EndTime) - Position, field.Unit,
                holding: HitObject.StartTime < field.CurrentTime - ShredChordRules.ChordTolerance, selected: EditorSelected, editor: true);
        }
    }
    public partial class DrawableShredEditorHead : DrawableHitObject<ShredHoldHead>
    {
        public DrawableShredEditorHead(ShredHoldHead note) : base(note) { }
        // The editor auditions the parent once at StartTime. Its nested gameplay
        // judgement never plays, so loading a second sample set wastes channels.
        public override IEnumerable<osu.Game.Audio.HitSampleInfo> GetSamples()
            => Array.Empty<osu.Game.Audio.HitSampleInfo>();
    }
}
