using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Rulesets.Shred.Objects;
using osuTK.Input;

namespace osu.Game.Rulesets.Shred.Edit
{
    public partial class ShredBlueprintContainer
    {
        private readonly List<ShredHitObject> clipboard = new List<ShredHitObject>();
        private ShredHitObject rangeAnchor;
        private ShredHitObject controlAnchor;
        private bool modifierClick;
        private ShredSelectionBlueprint deselectOnClick;
        private int copiedRow;

        public void ResetControlAnchor() => controlAnchor = null;

        private bool handleRangeSelection(MouseDownEvent e)
        {
            modifierClick = false;
            deselectOnClick = null;
            if (e.Button != MouseButton.Left || e.AltPressed || e.SuperPressed) return false;
            if (!e.ControlPressed) controlAnchor = null;
            var target = SelectionBlueprints.AliveChildren.OfType<ShredSelectionBlueprint>()
                .Where(b => b.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
                .OrderByDescending(b => b.HitObject.StartTime).FirstOrDefault();
            if (target == null) return false;
            if (e.ShiftPressed)
            {
                ShredHitObject anchor = (e.ControlPressed ? controlAnchor : rangeAnchor) ?? rangeAnchor ?? target.HitObject;
                double from = Math.Min(anchor.StartTime, target.HitObject.StartTime);
                double to = Math.Max(anchor.StartTime, target.HitObject.StartTime);
                if (!e.ControlPressed) DeselectAll();
                ShredEditorOperations.SelectRange(SelectionBlueprints.AliveChildren.OfType<ShredSelectionBlueprint>(),
                    b => b.HitObject.StartTime, from, to, b => b.Select());
                modifierClick = true;
                return true;
            }
            rangeAnchor = target.HitObject;
            if (!e.ControlPressed)
            {
                if (target.IsSelected) deselectOnClick = target;
                return false;
            }
            controlAnchor = target.HitObject;
            target.ToggleSelection();
            modifierClick = true;
            return true;
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (deselectOnClick != null && e.Button == MouseButton.Left)
            {
                deselectOnClick.Deselect();
                deselectOnClick = null;
                return true;
            }
            return modifierClick || base.OnClick(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (!modifierClick) base.OnMouseUp(e);
        }

        protected override void OnKeyUp(KeyUpEvent e)
        {
            if (e.Key == Key.ControlLeft || e.Key == Key.ControlRight) ResetControlAnchor();
            base.OnKeyUp(e);
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
                copiedRow = composer.Grid.ActiveRow;
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
                double end = source.Max(n => n.EndTime);
                target = composer.Grid.NextStep(end);
            }
            double cursor = EditorClock.CurrentTimeAccurate;
            var added = new List<ShredHitObject>();
            Beatmap.BeginChange();
            foreach (var item in source.OrderBy(n => n.StartTime))
            {
                ShredHitObject note = clone(item);
                if (key != Key.D && !note.IsWham && copiedRow != composer.Grid.ActiveRow) note.Row = 1 - note.Row;
                note.StartTime = Math.Max(0, target + item.StartTime - origin);
                composer.TrimSustainsAt(note);
                foreach (var duplicate in Beatmap.HitObjects.OfType<ShredHitObject>()
                    .Where(n => n.IsWham == note.IsWham && (note.IsWham || n.Column == note.Column)
                        && Math.Abs(n.StartTime - note.StartTime) < 0.5).ToArray()) Beatmap.Remove(duplicate);
                note.ApplyDefaults(Beatmap.ControlPointInfo, Beatmap.Difficulty);
                Beatmap.Add(note);
                added.Add(note);
            }
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
