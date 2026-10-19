using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Testing;
using osu.Game.Rulesets.Edit.Tools;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Shred.Edit
{
    // Share modifier gestures and the most recently selected group with the
    // playfield. Unmodified timeline movement and context menus stay native.
    public partial class ShredTimelineSelection : Container
    {
        private readonly Timeline timeline;
        private readonly EditorBlueprintContainer blueprints;
        private readonly EditorBeatmap beatmap;
        private readonly ShredHitObjectComposer composer;
        private readonly DragBox dragBox = new DragBox();
        private ShredBlueprintContainer selection => (ShredBlueprintContainer)composer.BlueprintContainer;
        private bool modifierGesture;
        private bool dragging;
        private HitObject toggleOnClick;
        private HitObject[] group = Array.Empty<HitObject>();
        private double startTime;
        private Vector2 cursor;

        private ShredTimelineSelection(Timeline timeline, EditorBlueprintContainer blueprints, EditorBeatmap beatmap, ShredHitObjectComposer composer)
        {
            this.timeline = timeline;
            this.blueprints = blueprints;
            this.beatmap = beatmap;
            this.composer = composer;
            RelativeSizeAxes = Axes.Both;
            Depth = float.MinValue;
            Add(dragBox);
        }

        public static bool Install(Editor editor, EditorBeatmap beatmap)
        {
            if (editor == null) return false;
            var composer = editor.ChildrenOfType<ShredHitObjectComposer>().FirstOrDefault();
            if (composer == null) return false;
            foreach (var timeline in editor.ChildrenOfType<Timeline>())
            {
                var blueprints = timeline.ChildrenOfType<EditorBlueprintContainer>().FirstOrDefault();
                if (blueprints == null || timeline.Parent is not Container parent) continue;
                if (!parent.Children.OfType<ShredTimelineSelection>().Any())
                    parent.Add(new ShredTimelineSelection(timeline, blueprints, beatmap, composer));
                return true;
            }
            return false;
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => timeline.IsPresent && timeline.ReceivePositionalInputAt(screenSpacePos);

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            modifierGesture = false;
            toggleOnClick = null;
            var target = blueprints.SelectionBlueprints.AliveChildren
                .Where(b => b.Item is ShredHitObject && b.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
                .OrderByDescending(b => b.Item.StartTime).FirstOrDefault()?.Item;
            if (e.Button == MouseButton.Right && composer.BlueprintContainer.CurrentTool is not SelectTool && target != null)
            {
                beatmap.BeginChange();
                beatmap.Remove(target);
                beatmap.EndChange();
                return true;
            }
            if (e.Button != MouseButton.Left || e.AltPressed || e.SuperPressed) return false;
            if (!e.ControlPressed && !e.ShiftPressed)
            {
                if (target != null) selection.RememberSelectionGroup(new[] { target });
                return false;
            }
            composer.SetSelectTool();
            selection.BeginModifierSelection(e.ShiftPressed);
            modifierGesture = true;
            startTime = timeAt(e.ScreenSpaceMousePosition);
            group = Array.Empty<HitObject>();
            if (target == null) return true;
            group = new[] { target };
            if (!e.ShiftPressed && beatmap.SelectedHitObjects.Contains(target)) toggleOnClick = target;
            selection.UpdateModifierSelection(group);
            return true;
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (!modifierGesture || e.Button != MouseButton.Left) return false;
            if (toggleOnClick != null) beatmap.SelectedHitObjects.Remove(toggleOnClick);
            else selection.CompleteModifierSelection(group);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (!modifierGesture || e.Button != MouseButton.Left) return false;
            toggleOnClick = null;
            dragging = true;
            dragBox.Show();
            updateDrag(e);
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (dragging) updateDrag(e);
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            if (!dragging) return;
            updateDrag(e);
            selection.CompleteModifierSelection(group);
            dragging = false;
            dragBox.Hide();
        }

        private double timeAt(Vector2 screen) => timeline.TimeAtPosition(blueprints.ToLocalSpace(screen).X);

        private void updateDrag(MouseButtonEvent e)
        {
            e.Target = this;
            dragBox.HandleDrag(e);
            cursor = e.ScreenSpaceMousePosition;
            updateGroup();
        }

        private void updateGroup()
        {
            double endTime = timeAt(cursor);
            double from = Math.Min(startTime, endTime) - ShredChordRules.ChordTolerance;
            double to = Math.Max(startTime, endTime) + ShredChordRules.ChordTolerance;
            group = beatmap.HitObjects.Where(n => n.StartTime >= from && n.StartTime <= to).ToArray();
            selection.UpdateModifierSelection(group);
        }

        protected override void Update()
        {
            base.Update();
            if (!dragging) return;
            float x = timeline.ToLocalSpace(cursor).X;
            float amount = 0;
            if (x < 40) amount = x - 40;
            else if (x > timeline.DrawWidth - 40) amount = x - timeline.DrawWidth + 40;
            if (amount == 0) return;
            timeline.ScrollBy(Math.Clamp(amount / 20, -6, 6) * Time.Elapsed);
            updateGroup();
        }
    }
}
