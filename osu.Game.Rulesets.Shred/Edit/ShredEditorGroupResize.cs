using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Shred.Objects;
using osuTK;

namespace osu.Game.Rulesets.Shred.Edit
{
    public partial class ShredBlueprintContainer
    {
        private readonly Dictionary<ShredHitObject, (double start, double end, double minimum)> resizeOrigins = new();
        private double resizeAnchor;
        private double resizeGrabTime;

        private void beginGroupResize(ShredSelectionBlueprint target, Vector2 down, Vector2 cursor)
        {
            resizingNote = target.HitObject;
            resizeColumn = resizingNote.Column;
            if (resizingNote.IsWham) resizeColumn = 12;
            resizeAnchor = resizingNote.EndTime;
            resizeGrabTime = composer.Grid.TimeAt(down, resizeColumn);
            resizeCursor = cursor;
            resizeOrigins.Clear();
            IEnumerable<ShredHitObject> notes = new[] { resizingNote };
            if (target.IsSelected) notes = SelectedItems.OfType<ShredHitObject>().Where(n => n.IsLong);
            foreach (ShredHitObject note in notes)
                resizeOrigins[note] = (note.StartTime, note.EndTime,
                    note.IsWham ? 1 : 0);
            Beatmap.BeginChange();
            updateResize();
        }

        private void updateResize()
        {
            double movement = composer.Grid.TimeAt(resizeCursor, resizeColumn) - resizeGrabTime;
            // Snap the grabbed endpoint once. Apply one shared time offset to
            // both rows, preserving the selection's existing rhythmic offsets.
            double delta = composer.SnapTime(resizeAnchor + movement) - resizeAnchor;
            delta = Math.Max(delta, resizeOrigins.Values.Max(o => o.start + o.minimum - o.end));
            foreach (var pair in resizeOrigins)
            {
                ShredHitObject note = pair.Key;
                double start = pair.Value.start;
                double end = pair.Value.end;
                end += delta;
                if (Math.Abs(note.StartTime - start) < 0.001 && Math.Abs(note.EndTime - end) < 0.001) continue;
                note.StartTime = start;
                note.Duration = end - start;
                note.ApplyDefaults(Beatmap.ControlPointInfo, Beatmap.Difficulty);
                Beatmap.Update(note);
            }
        }

        private void finishResize()
        {
            composer.ResolveTailConflicts(resizeOrigins.Keys);
            resizingNote = null;
            resizeOrigins.Clear();
            Beatmap.EndChange();
        }
    }
}
