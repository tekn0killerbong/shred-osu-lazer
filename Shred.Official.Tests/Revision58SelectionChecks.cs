using osu.Framework.Allocation;
using osu.Framework.Input.StateChanges;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osuTK;
using osuTK.Input;

internal partial class Revision54EditorRateChecks
{
    private IEnumerable<Func<bool>> drag58(Vector2 from, Vector2 to)
    {
        new MousePositionAbsoluteInput { Position = from }.Apply(pointerInput.CurrentState, pointerInput);
        yield return () => true;
        new MouseButtonInput(MouseButton.Left, true).Apply(pointerInput.CurrentState, pointerInput);
        yield return () => true;
        new MousePositionAbsoluteInput { Position = (from + to) / 2 }.Apply(pointerInput.CurrentState, pointerInput);
        yield return () => true;
        new MousePositionAbsoluteInput { Position = to }.Apply(pointerInput.CurrentState, pointerInput);
        yield return () => true;
        new MouseButtonInput(MouseButton.Left, false).Apply(pointerInput.CurrentState, pointerInput);
        yield return () => true;
    }

    private IEnumerable<Func<bool>> tailHandleSteps(Editor editor)
    {
        var composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
        var map = editor.Dependencies.Get<EditorBeatmap>();
        var clock = editor.Dependencies.Get<EditorClock>();
        var history = editor.Dependencies.Get<IEditorChangeHandler>();
        composer.StepRecorder.Enabled.Value = false; composer.SetSelectTool();
        map.TimelineZoom = 0.4;
        foreach (int row in new[] { 0, 1 })
        foreach (int divisor in new[] { 4, 6, 12 })
        foreach (bool occupied in new[] { false, true })
        {
            clock.Stop(); clock.Seek(1250);
            composer.Dependencies.Get<BindableBeatDivisor>().SetArbitraryDivisor(divisor);
            map.BeginChange();
            foreach (var note in map.HitObjects.ToArray()) map.Remove(note);
            foreach (var group in map.ControlPointInfo.Groups.ToArray()) map.ControlPointInfo.RemoveGroup(group);
            map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            var sustain = new ShredHitObject { StartTime = 1000, Duration = 1000, Row = row, Fret = 2 };
            var partner = new ShredHitObject { StartTime = 1000, Duration = 1000, Row = 1 - row, Fret = 4 };
            var atTail = new ShredHitObject { StartTime = 2000, Row = row, Fret = 2 };
            map.Add(sustain); map.Add(partner); if (occupied) map.Add(atTail);
            map.EndChange();
            map.SelectedHitObjects.Clear(); map.SelectedHitObjects.AddRange(new[] { sustain, partner });
            yield return () => true;
            var grid = composer.Grid;
            Vector2 screen(double time) => grid.ToScreenSpace(grid.PositionAt(sustain.Column, time));
            string label = $"tail row={row}, grid=1/{divisor}, occupied={occupied}";
            double halfHeadTime = grid.NoteSize(2).Y / 2 / Math.Abs(grid.Travel(row).Y) * grid.VisibleTime;
            double edge = occupied ? 2000 - halfHeadTime : 2000;
            double precedingQuarter = (Math.Ceiling(edge / 125 - 0.000001) - 1) * 125;
            double handleTime = (precedingQuarter + edge) / 2;
            check(grid.EndHandleContains(sustain, screen(handleTime)), label + " has a resize zone before the tail head even when zoomed out");
            check(!grid.EndHandleContains(sustain, screen(precedingQuarter - 5)) && grid.NoteContains(sustain, screen(precedingQuarter - 5)),
                label + " body before the previous common 1/4 tick stays movable");
            check(grid.EndHandleContains(sustain, screen(2000)) == !occupied,
                label + " resize reaches the end only when no head occupies it");
            if (occupied)
            {
                Vector2 size = grid.NoteSize(2);
                Vector2 direction = grid.Skin.Direction(row);
                foreach (float x in new[] { -0.49f, 0f, 0.49f })
                foreach (float y in new[] { -0.49f, 0f, 0.49f })
                {
                    Vector2 point = grid.ToScreenSpace(grid.PositionAt(sustain.Column, 2000)
                        + new Vector2(x * size.X + y * size.Y * direction.X / direction.Y, y * size.Y));
                    check(grid.HeadContains(atTail, point) && grid.NoteContains(atTail, point)
                        && !grid.NoteContains(sustain, point) && !grid.EndHandleContains(sustain, point),
                        label + $" entire tail note owns point {x},{y}");
                }
            }
            foreach (var step in drag58(screen(handleTime), screen(handleTime - 250))) yield return step;
            check(sustain.StartTime == 1000 && sustain.EndTime == 1750 && partner.StartTime == 1000 && partner.EndTime == 1750,
                label + " dragging the zone resizes all selected LNs without moving their heads");
            if (occupied) check(atTail.StartTime == 2000, label + " resize does not move the overlapping note");
            history.RestoreState(-1);
            yield return () => true;
            check(map.HitObjects.OfType<ShredHitObject>().Where(n => n.IsLong).All(n => n.StartTime == 1000 && n.EndTime == 2000), label + " undo restores all lengths");
            history.RestoreState(1);
            yield return () => true;
            check(map.HitObjects.OfType<ShredHitObject>().Where(n => n.IsLong).All(n => n.StartTime == 1000 && n.EndTime == 1750), label + " redo restores resize");
        }
        // Real pointer movement from the downstream edge of a note lying on a tail.
        foreach (int row in new[] { 0, 1 })
        foreach (bool body in new[] { false, true })
        {
            composer.Dependencies.Get<BindableBeatDivisor>().SetArbitraryDivisor(4);
            clock.Seek(750);
            map.BeginChange();
            foreach (var note in map.HitObjects.ToArray()) map.Remove(note);
            var sustain = new ShredHitObject { StartTime = 1000, Duration = 1000, Row = row, Fret = 2 };
            var noteAtEnd = new ShredHitObject { StartTime = 2000, Row = row, Fret = 2 };
            map.Add(sustain); if (!body) map.Add(noteAtEnd); map.EndChange();
            map.SelectedHitObjects.Clear(); map.SelectedHitObjects.Add(sustain);
            yield return () => true;
            var grid = composer.Grid;
            double grab = body ? 1500 : 2000;
            var from = grid.ToScreenSpace(grid.PositionAt(sustain.Column, grab));
            var to = grid.ToScreenSpace(grid.PositionAt(sustain.Column, grab + 125));
            foreach (var step in drag58(from, to)) yield return step;
            if (!body)
                check(sustain.StartTime == 1000 && sustain.EndTime == 2000 && noteAtEnd.StartTime == 2125
                    && map.SelectedHitObjects.SequenceEqual(new[] { noteAtEnd }), "dragging a tail note moves that note, not the previously selected LN");
            else
                check(sustain.StartTime == 1125 && sustain.Duration == 1000,
                    "dragging the rest of the LN body moves the complete note without resizing");
        }
    }

    private IEnumerable<Func<bool>> timelineSelectionSteps(Editor editor)
    {
        var composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
        var map = editor.Dependencies.Get<EditorBeatmap>();
        var clock = editor.Dependencies.Get<EditorClock>();
        var history = editor.Dependencies.Get<IEditorChangeHandler>();
        composer.StepRecorder.Enabled.Value = false; composer.SetSelectTool();
        clock.Stop(); clock.Seek(3000); map.TimelineZoom = 1;
        map.BeginChange();
        foreach (var note in map.HitObjects.ToArray()) map.Remove(note);
        var notes = Enumerable.Range(0, 5).SelectMany(i => new[]
        {
            new ShredHitObject { StartTime = 2000 + i * 500, Row = 0, Fret = 1 },
            new ShredHitObject { StartTime = 2000 + i * 500, Row = 1, Fret = 2, Duration = 250 },
            new ShredHitObject { StartTime = 2000 + i * 500 + (i % 2 == 0 ? 0.2 : -0.2), Row = 0, Fret = 3, Duration = 3000 }
        }).ToArray();
        foreach (var note in notes) map.Add(note);
        map.EndChange();
        // Ensure the last chord has a long member extending beyond the next row.
        map.BeginChange(); notes[^1].Duration = 3000; map.Update(notes[^1]); map.EndChange();
        map.SelectedHitObjects.Clear(); map.SelectedHitObjects.Add(notes[6]);
        yield return () => editor.ChildrenOfType<ShredTimelineSelection>().Any();
        DateTime layoutReady = DateTime.UtcNow.AddMilliseconds(400);
        yield return () => DateTime.UtcNow >= layoutReady;
        foreach (int row in new[] { 4, 0, 3, 1 })
        {
            var before = map.SelectedHitObjects.ToHashSet();
            var blueprint = editor.ChildrenOfType<TimelineHitObjectBlueprint>().Single(b => b.Item == notes[row * 3]);
            Vector2 point = blueprint.ToScreenSpace(new Vector2(0, blueprint.DrawHeight / 2));
            revision57Key(Key.ShiftLeft, true);
            new MousePositionAbsoluteInput { Position = point }.Apply(pointerInput.CurrentState, pointerInput);
            yield return () => true;
            revision57Click(point);
            yield return () => true;
            revision57Key(Key.ShiftLeft, false);
            yield return () => true;
            int first = row == 4 ? 2 : 0;
            var expected = notes.Skip(first * 3).ToHashSet();
            check(map.SelectedHitObjects.ToHashSet().SetEquals(expected), "upper timeline Shift+click " + row
                + " includes complete rows at both ends, long tails and rounding; times=" + string.Join(",", map.SelectedHitObjects.Select(n => n.StartTime)));
            check(close(clock.CurrentTimeAccurate, 3000), "timeline range selection never seeks the grid");
            if (!before.SetEquals(expected))
            {
                history.RestoreState(-1);
                yield return () => true;
                check(map.SelectedHitObjects.ToHashSet().SetEquals(before), "timeline selection extension is one undo");
                history.RestoreState(1);
                yield return () => true;
                check(map.SelectedHitObjects.ToHashSet().SetEquals(expected), "redo restores every timeline chord member");
            }
        }
    }
}
