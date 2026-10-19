using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.GameplayTest;

namespace osu.Game.Rulesets.Shred.Compatibility
{
    public partial class ShredEditorSession : Component
    {
        private static readonly ConditionalWeakTable<EditorBeatmap, ShredEditorHistory> histories = new ConditionalWeakTable<EditorBeatmap, ShredEditorHistory>();
        [Resolved] private EditorBeatmap beatmap { get; set; }
        [Resolved(canBeNull: true)] private Editor editor { get; set; }
        private ScreenStack stack;
        private bool testing;
        private ShredEditorHistory selectionHistory;
		private Edit.ShredHitObjectComposer composer;
        private bool timelineSelectionInstalled;
        protected override void Update()
        {
            base.Update();
            if (!timelineSelectionInstalled) timelineSelectionInstalled = Edit.ShredTimelineSelection.Install(editor, beatmap);
        }
        [BackgroundDependencyLoader]
        private void load(IEditorChangeHandler changeHandler, osu.Game.Beatmaps.BeatmapManager manager, osu.Framework.Platform.Storage storage, Edit.ShredHitObjectComposer composer)
        {
			this.composer = composer;
#if SHRED_OFFICIAL
            ShredLegacyExport.Install(manager, storage);
#endif
            if (changeHandler is EditorChangeHandler history)
            {
                selectionHistory = histories.GetValue(beatmap, b => new ShredEditorHistory(b, history));
            }
            beatmap.HitObjectAdded += trimInsertedSustains;
        }
        private void trimInsertedSustains(HitObject hitObject)
        {
            // Native Ctrl+V and menu Paste bypass the Shred placement hooks.
            // Added callbacks run before history commits the transaction, so the
            // cut and paste remain one undo. Never modify a restored snapshot.
            if (selectionHistory?.CanProcessInsertions != true || hitObject is not ShredHitObject note) return;
            if (!beatmap.HitObjects.Contains(note)) return;
            composer.ResolveTailConflicts(new[] { note });
        }

        public void FinishInputFrame()
        {
            var mouse = GetContainingInputManager()?.CurrentState.Mouse;
            bool dragging = mouse != null && (mouse.Buttons.IsPressed(osuTK.Input.MouseButton.Left)
                || mouse.Buttons.IsPressed(osuTK.Input.MouseButton.Right));
            selectionHistory?.FinishFrame(dragging);
        }
        protected override void LoadComplete()
        {
            base.LoadComplete();
            for (Drawable parent = editor; parent != null; parent = parent.Parent)
            {
                if (parent is not ScreenStack found) continue;
                stack = found;
                stack.ScreenPushed += pushed;
                stack.ScreenExited += exited;
                break;
            }
        }
        private void pushed(IScreen source, IScreen destination)
        {
            if (source == editor && destination is EditorPlayerLoader) testing = true;
        }
        private void exited(IScreen source, IScreen destination)
        {
			// Flush on the update thread before another difficulty can open.
			if (source == editor) composer.StepRecorder?.FinishGridMemory();
            if (!testing || destination != editor) return;
            testing = false;
            beatmap.SelectedHitObjects.Clear();
        }
        protected override void Dispose(bool isDisposing)
        {
            if (beatmap != null) beatmap.HitObjectAdded -= trimInsertedSustains;
            if (stack != null)
            {
                stack.ScreenPushed -= pushed;
                stack.ScreenExited -= exited;
            }
            base.Dispose(isDisposing);
        }
    }
}
