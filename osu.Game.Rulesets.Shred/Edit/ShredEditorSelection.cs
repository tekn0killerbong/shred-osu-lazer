using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Gameplay;
using osuTK.Input;

namespace osu.Game.Rulesets.Shred.Edit
{
    public partial class ShredBlueprintContainer
    {
        private readonly List<ShredHitObject> clipboard = new List<ShredHitObject>();
        private readonly HashSet<HitObject> groupAnchor = new HashSet<HitObject>();
        private readonly HashSet<HitObject> marqueeGroup = new HashSet<HitObject>();
        private HashSet<HitObject> selectionBeforeGesture = new HashSet<HitObject>();
        private bool modifierClick;
        private bool modifierMarquee;
        private bool rangeGesture;
        private bool tailHandleClick;
        private ShredSelectionBlueprint deselectOnClick;
        private ShredSelectionBlueprint controlToggleOnClick;

        private bool handleRangeSelection(MouseDownEvent e)
        {
            modifierClick = false;
            tailHandleClick = false;
            deselectOnClick = null;
            controlToggleOnClick = null;
            if (e.Button != MouseButton.Left || e.AltPressed || e.SuperPressed) return false;
            if (!composer.Grid.ContainsScreen(e.ScreenSpaceMousePosition)) return false;
            var target = SelectionBlueprints.AliveChildren.OfType<ShredSelectionBlueprint>()
                .Where(b => b.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
                .OrderByDescending(b => b.HitObject.StartTime).FirstOrDefault();
            if (e.ControlPressed || e.ShiftPressed)
            {
                composer.SetSelectTool();
                modifierClick = true;
                BeginModifierSelection(e.ShiftPressed);
                if (target == null) return true;
                if (rangeGesture)
                {
                    var selected = new HashSet<HitObject>(selectionBeforeGesture) { target.HitObject };
                    addRange(selected, new[] { target.HitObject });
                    applySelection(selected);
                    if (groupAnchor.Count == 0) RememberSelectionGroup(new[] { target.HitObject });
                }
                else
                {
                    if (!target.IsSelected) target.Select();
                    else controlToggleOnClick = target;
                    RememberSelectionGroup(new[] { target.HitObject });
                }
                return true;
            }
            if (target == null) return false;
            if (SelectionBlueprints.Children.OfType<ShredSelectionBlueprint>()
                .Any(b => b.IsSelected && composer.Grid.EndHandleContains(b.HitObject, e.ScreenSpaceMousePosition)))
            {
                tailHandleClick = true;
                deselectOnClick = target;
                return true;
            }
            RememberSelectionGroup(new[] { target.HitObject });
            if (target.IsSelected) deselectOnClick = target;
            else DeselectAll();
            return false;
        }

        internal void RememberSelectionGroup(IEnumerable<HitObject> notes)
        {
            HitObject[] group = notes.ToArray();
            if (group.Length == 0) return;
            groupAnchor.Clear();
            groupAnchor.UnionWith(group);
        }

        // Shared by the playfield and its upper timeline, so the last selected
        // group remains the range anchor when moving between the two views.
        internal void BeginModifierSelection(bool range)
        {
            rangeGesture = range;
            selectionBeforeGesture = new HashSet<HitObject>(SelectedItems);
            groupAnchor.RemoveWhere(n => !selectionBeforeGesture.Contains(n));
            if (groupAnchor.Count == 0) groupAnchor.UnionWith(selectionBeforeGesture);
        }

        internal void UpdateModifierSelection(IEnumerable<HitObject> group)
        {
            HitObject[] notes = group.ToArray();
            var selected = new HashSet<HitObject>(selectionBeforeGesture);
            selected.UnionWith(notes);
            if (rangeGesture) addRange(selected, notes);
            applySelection(selected);
        }

        internal void CompleteModifierSelection(IEnumerable<HitObject> group)
        {
            if (!rangeGesture || groupAnchor.Count == 0) RememberSelectionGroup(group);
        }

        private void addRange(HashSet<HitObject> selected, IEnumerable<HitObject> endpoint)
        {
            HitObject[] end = endpoint.ToArray();
            if (end.Length == 0) return;
            IEnumerable<HitObject> bounds = groupAnchor.Concat(end);
            double from = bounds.Min(n => n.StartTime) - ShredChordRules.ChordTolerance;
            double to = bounds.Max(n => n.StartTime) + ShredChordRules.ChordTolerance;
            // Include complete temporal rows at both ends, across both fret rows.
            selected.UnionWith(Beatmap.HitObjects.Where(n => n.StartTime >= from && n.StartTime <= to));
        }

        private void applySelection(HashSet<HitObject> selected)
        {
            foreach (HitObject note in Beatmap.SelectedHitObjects.ToArray())
                if (!selected.Contains(note)) Beatmap.SelectedHitObjects.Remove(note);
            foreach (HitObject note in selected.OrderBy(n => n.StartTime))
                if (!Beatmap.SelectedHitObjects.Contains(note)) Beatmap.SelectedHitObjects.Add(note);
        }

        private bool beginModifierMarquee(DragStartEvent e)
        {
            deselectOnClick = null;
            controlToggleOnClick = null;
            modifierMarquee = true;
            selecting = true;
            additiveSelection = new HashSet<HitObject>(selectionBeforeGesture);
            dragOriginRow = composer.Grid.RowAt(e.ScreenSpaceMouseDownPosition);
            selectionViewTime = composer.Grid.CurrentTime;
            selectionStartPoint = composer.Grid.ToLocalSpace(e.ScreenSpaceMouseDownPosition);
            selectionStartTime = composer.Grid.TimeAt(e.ScreenSpaceMouseDownPosition, dragOriginRow * 6);
            selectionColumn = composer.Grid.ColumnAt(e.ScreenSpaceMouseDownPosition);
            if (selectionColumn < 0) selectionColumn = dragOriginRow * 6;
            DragBox.Show();
            updateModifierMarquee(e);
            return true;
        }

        private void updateModifierMarquee(MouseButtonEvent e)
        {
            e.Target = this;
            DragBox.HandleDrag(e);
            selectionCursor = e.ScreenSpaceMousePosition;
            captureSelectionEnd();
            UpdateSelectionFromDragBox(additiveSelection);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (deselectOnClick != null && e.Button == MouseButton.Left)
            {
                var only = deselectOnClick;
                DeselectAll();
                only.Select();
                RememberSelectionGroup(new[] { only.HitObject });
                deselectOnClick = null;
                return true;
            }
            if (controlToggleOnClick != null && e.Button == MouseButton.Left)
            {
                controlToggleOnClick.Deselect();
                groupAnchor.Remove(controlToggleOnClick.HitObject);
                controlToggleOnClick = null;
                return true;
            }
            if (modifierClick && e.Button == MouseButton.Left) return true;
            if (e.Button == MouseButton.Left && !SelectionBlueprints.AliveChildren.OfType<ShredSelectionBlueprint>()
                .Any(b => b.ReceivePositionalInputAt(e.ScreenSpaceMousePosition)))
            {
                if (composer.Grid.ContainsScreen(e.ScreenSpaceMousePosition))
                {
                    DeselectAll();
                    groupAnchor.Clear();
                }
                return true;
            }
            return tailHandleClick || base.OnClick(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (!modifierClick && !tailHandleClick) base.OnMouseUp(e);
        }

        private static ShredHitObject clone(ShredHitObject note) => new ShredHitObject
        {
            StartTime = note.StartTime, Duration = note.Duration,
            Row = note.Row, Fret = note.Fret, Articulation = note.Articulation,
            Samples = note.Samples.Select(s => s.With()).ToList()
        };

        public void ClipboardCommand(Key key)
        {
            ShredHitObject[] selected = SelectedItems.OfType<ShredHitObject>().OrderBy(n => n.StartTime).ToArray();
            if (key == Key.C || key == Key.X)
            {
                if (selected.Length == 0) return;
                clipboard.Clear();
                clipboard.AddRange(selected.Select(clone));
                if (key == Key.C) return;
                Beatmap.BeginChange();
                foreach (var note in selected) Beatmap.Remove(note);
                Beatmap.EndChange();
                return;
            }
            ShredHitObject[] source = key == Key.D ? selected : clipboard.ToArray();
            if (source.Length == 0) return;
            double origin = source.Min(n => n.StartTime);
            double target = composer.SnapTime(EditorClock.CurrentTimeAccurate);
            if (key == Key.D)
            {
				target = source.Max(note => note.EndTime);
				// A tail can touch the next head. Only a short note at the final
				// time needs a step of spacing before the duplicated pattern.
				if (source.Any(note => !note.IsLong && note.StartTime == target))
					target = composer.Grid.NextStep(target);
            }
            double cursor = EditorClock.CurrentTimeAccurate;
            var added = new List<ShredHitObject>();
            Beatmap.BeginChange();
            foreach (var item in source.OrderBy(n => n.StartTime))
            {
                ShredHitObject note = clone(item);
                note.StartTime = Math.Max(0, target + item.StartTime - origin);
                composer.TrimSustainsAt(note);
                foreach (var duplicate in Beatmap.HitObjects.OfType<ShredHitObject>()
                    .Where(n => n.IsWham == note.IsWham && (note.IsWham || n.Column == note.Column)
                        && ShredChordRules.SameChordTime(n.StartTime, note.StartTime)).ToArray()) Beatmap.Remove(duplicate);
                note.ApplyDefaults(Beatmap.ControlPointInfo, Beatmap.Difficulty);
                Beatmap.Add(note);
                added.Add(note);
            }
            composer.ResolveTailConflicts(added);
            Beatmap.EndChange();
            EditorClock.Seek(cursor);
            composer.SetSelectTool();
            Scheduler.Add(() =>
            {
                DeselectAll();
                foreach (var note in added) GetBlueprintFor(note)?.Select();
            });
        }
    }
}
