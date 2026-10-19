using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Testing;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.UI;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Input;

internal static class Revision44UiChecks
{
    private static object Invoke(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(o, args)!;
    private static InputState State(Vector2 p, Key? key = null)
    {
        var state = new InputState(); state.Mouse.Position = p;
        if (key.HasValue) state.Keyboard.Keys.SetPressed(key.Value, true);
        return state;
    }
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS v44 " + label); }
    private static void Close(double expected, double actual, string label) => Check(Math.Abs(expected - actual) < 0.01, $"{label}: expected {expected}, actual {actual}");
    private static void KeyDown(ShredStepRecorder r, Key key)
        => Invoke(r, "OnKeyDown", new KeyDownEvent(State(Vector2.Zero, key), key));
    private static void KeyUp(ShredStepRecorder r, Key key)
        => Invoke(r, "OnKeyUp", new KeyUpEvent(State(Vector2.Zero), key));
    private static void DragEnd(ShredHitObjectComposer c, ShredHitObject n, double end)
    {
        var b = (ShredBlueprintContainer)c.BlueprintContainer;
        var pad = new Vector2(c.Grid.NoteSize(n.Fret).X / 2 + 2 * c.Grid.Unit, 0);
        var p = c.Grid.ToScreenSpace(c.Grid.PositionAt(n.Column, n.EndTime) + pad);
        var q = c.Grid.ToScreenSpace(c.Grid.PositionAt(n.Column, end) + pad);
        Check(c.Grid.EndHandleContains(n, p), "subdivision test grabs tail wing");
        foreach (var blueprint in b.ChildrenOfType<ShredSelectionBlueprint>())
            typeof(Drawable).GetProperty("IsHovered")!.SetValue(blueprint, blueprint.ReceivePositionalInputAt(p));
        Invoke(b, "OnMouseDown", new MouseDownEvent(State(p), MouseButton.Left));
        Invoke(b, "OnDragStart", new DragStartEvent(State(q), MouseButton.Left, p));
        Invoke(b, "OnDrag", new DragEvent(State(q), MouseButton.Left, p, p));
        Invoke(b, "OnDragEnd", new DragEndEvent(State(q), MouseButton.Left, p));
        Invoke(b, "OnMouseUp", new MouseUpEvent(State(q), MouseButton.Left));
    }
    public static IEnumerable<Action> Steps(Editor editor)
    {
        var c = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
        var b = (ShredBlueprintContainer)c.BlueprintContainer;
        var map = c.Dependencies.Get<EditorBeatmap>();
        var clock = c.Dependencies.Get<EditorClock>();
        var divisor = c.Dependencies.Get<BindableBeatDivisor>();
        var history = c.Dependencies.Get<IEditorChangeHandler>();
        var note = new ShredHitObject { StartTime = 1000, Fret = 2, Duration = 1000 };
        void Clear()
        {
            map.SelectedHitObjects.Clear(); map.BeginChange();
            foreach (var n in map.HitObjects.ToArray()) map.Remove(n);
            map.EndChange();
        }
        yield return () =>
        {
            c.StepRecorder.Enabled.Value = false; Clear();
            map.BeginChange(); map.Add(note); map.EndChange();
            map.TimelineZoom = 0.5; divisor.SetArbitraryDivisor(4); c.SetSelectTool(); clock.Seek(1000);
        };
        yield return () =>
        {
            var size = c.Grid.ToLocalSpace(editor.ToScreenSpace(editor.DrawSize)) - c.Grid.ToLocalSpace(editor.ToScreenSpace(Vector2.Zero));
            using var gameplay = new ShredPlayfield { RelativeSizeAxes = Axes.None, Size = size };
            Close(gameplay.NoteSize(2).X, c.Grid.NoteSize(2).X, "editor fret width equals gameplay");
            Close(gameplay.NoteSize(2).Y, c.Grid.NoteSize(2).Y, "editor fret height equals gameplay");
            float left = Math.Min(c.Grid.Receiver(0, 1).X, c.Grid.Receiver(1, 1).X) - c.Grid.NoteSize(1).X / 2;
            float right = Math.Max(c.Grid.Receiver(0, 5).X, c.Grid.Receiver(1, 5).X) + c.Grid.NoteSize(5).X / 2;
            Close(c.Grid.DrawWidth / 2, (left + right) / 2, "both fret rows centered without fingerpicks");
            var drawable = c.ChildrenOfType<DrawableShredEditorNote>().Single(n => n.HitObject == note);
            Close(c.Grid.Receiver(0, 2).Y, drawable.Position.Y, "LN base occupies receptor at real start");
            clock.Seek(1500);
        };
        yield return () =>
        {
            var drawable = c.ChildrenOfType<DrawableShredEditorNote>().Single(n => n.HitObject == note);
            Check((drawable.Position - c.Grid.PositionAt(note.Column, note.StartTime)).Length < 0.01,
                "past LN head stays at actual time instead of following the receptor");
            Check(Math.Abs(drawable.Position.Y - c.Grid.Receiver(0, 2).Y) > c.Grid.NoteSize(2).Y,
                "past head lies outside receptor mask while extension remains alive");
            Check(drawable.Alpha == 1, "sustain remains visible after head passes");
            clock.Seek(777);
        };
        foreach (bool recording in new[] { false, true })
        {
            yield return () => { c.StepRecorder.Enabled.Value = recording; clock.Seek(777); map.SelectedHitObjects.Clear(); };
            yield return () =>
            {
                var n = map.HitObjects.OfType<ShredHitObject>().Single();
                var p = c.Grid.ToScreenSpace(c.Grid.PositionAt(n.Column, n.StartTime));
                Invoke(c.StepRecorder, "OnMouseDown", new MouseDownEvent(State(p), MouseButton.Right));
                Invoke(b, "OnMouseDown", new MouseDownEvent(State(p), MouseButton.Right));
                Check(map.HitObjects.Count == 0, "right click deletes the note");
                Close(777, clock.CurrentTimeAccurate, $"right delete does not seek, recorder {recording}");
            };
            yield return () =>
            {
                Close(777, clock.CurrentTimeAccurate, $"right delete stays stationary after recorder update, recorder {recording}");
                history.RestoreState(-1);
                Check(map.HitObjects.Count == 1, "undo restores deleted note");
                Close(777, clock.CurrentTimeAccurate, "undo of visible note does not seek");
            };
        }
        yield return () =>
        {
            c.StepRecorder.Enabled.Value = false;
            var n = map.HitObjects.Single(); map.SelectedHitObjects.Add(n);
            map.BeginChange(); map.Remove(n); map.EndChange();
            history.RestoreState(-1);
            Check(map.SelectedHitObjects.Count == 1, "undo restores selected note");
            Close(777, clock.CurrentTimeAccurate, "visible restored selection leaves grid in place");
            history.RestoreState(1); clock.Seek(5000);
        };
        yield return () =>
        {
            history.RestoreState(-1);
            Close(1000, clock.CurrentTimeAccurate, "offscreen restored selection still seeks to its beginning");
            clock.Seek(777); history.RestoreState(1);
            Close(777, clock.CurrentTimeAccurate, "redo never seeks");
        };
        foreach (int spacing in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 12, 16, 32, 64 })
        {
            ShredHitObject tiny = null;
            double head = 0, target = 0;
            yield return () =>
            {
                Clear(); divisor.SetArbitraryDivisor(spacing);
                double step = map.ControlPointInfo.TimingPointAt(1000).BeatLength / spacing;
                target = 1000 + step;
                head = 1000 + step * 2 / 3;
                tiny = new ShredHitObject { StartTime = head, Duration = step * 2 / 3, Fret = 3 };
                map.BeginChange(); map.Add(tiny); map.EndChange();
                map.SelectedHitObjects.Add(tiny); clock.Seek(900);
            };
            yield return () =>
            {
                DragEnd(c, tiny, target);
                Close(c.SnapTime(target), tiny.EndTime, $"tail shrinks below old minimum on 1/{spacing}");
                Close(head, tiny.StartTime, "off-grid head stays fixed");
            };
        }
        ShredHitObject mixed = null;
        yield return () =>
        {
            Clear(); divisor.SetArbitraryDivisor(12);
            mixed = new ShredHitObject { StartTime = 1000 + 500d / 12, Duration = 500d / 12, Fret = 2 };
            map.BeginChange(); map.Add(mixed); map.EndChange(); map.SelectedHitObjects.Add(mixed); clock.Seek(900);
            divisor.SetArbitraryDivisor(4);
        };
        yield return () =>
        {
            DragEnd(c, mixed, 1125); Close(1125, mixed.EndTime, "1/12 LN expands on current 1/4");
            DragEnd(c, mixed, 1000); Close(mixed.StartTime, mixed.EndTime, "1/4 shrink clamps at real 1/12 base, never before it");
            Check(!mixed.IsLong, "shrinking to base returns to short note");
        };
        yield return () =>
        {
            Clear(); divisor.SetArbitraryDivisor(12); c.StepRecorder.Enabled.Value = true; clock.Seek(1000 + 500d / 12);
            c.BlueprintContainer.CurrentTool = new ShredCompositionTool("Pick", global::osu.Game.Rulesets.Shred.Gameplay.ShredPracticeArticulation.Pick);
        };
        yield return () => { KeyDown(c.StepRecorder, Key.A); };
        // Wait longer than the removed default threshold through real editor updates.
        for (int i = 0; i < 5; i++) yield return () => { };
        yield return () =>
        {
            Check(map.HitObjects.Count == 1 && !map.HitObjects.OfType<ShredHitObject>().Single().IsLong,
                "held gameplay key remains short after more than one second");
            KeyDown(c.StepRecorder, Key.Slash); KeyUp(c.StepRecorder, Key.Slash);
        };
        yield return () =>
        {
            var n = map.HitObjects.OfType<ShredHitObject>().Single();
            Close(1000 + 1000d / 12, n.EndTime, "empty step starts held LN on 1/12");
            KeyDown(c.StepRecorder, Key.O); KeyUp(c.StepRecorder, Key.O);
            // Force a common spacing after exercising the actual shortcut route.
            divisor.SetArbitraryDivisor(4);
            KeyDown(c.StepRecorder, Key.Slash); KeyUp(c.StepRecorder, Key.Slash);
        };
        yield return () =>
        {
            var n = map.HitObjects.OfType<ShredHitObject>().Single();
            Close(1125, n.EndTime, "grid shortcut keeps held gesture; next empty step uses 1/4");
            KeyDown(c.StepRecorder, Key.Left); KeyUp(c.StepRecorder, Key.Left);
        };
        yield return () =>
        {
            var n = map.HitObjects.OfType<ShredHitObject>().Single();
            Close(n.StartTime, n.EndTime, "held LN shrinks to base on coarser grid");
            KeyDown(c.StepRecorder, Key.Slash); KeyUp(c.StepRecorder, Key.Slash);
        };
        yield return () => { KeyDown(c.StepRecorder, Key.Z); KeyUp(c.StepRecorder, Key.Z); };
        yield return () =>
        {
            Close(1125, clock.CurrentTimeAccurate, "releasing another row does not advance while first fret held");
            KeyUp(c.StepRecorder, Key.A);
        };
        yield return () =>
        {
            Close(1250, clock.CurrentTimeAccurate, "last release advances exactly one current step");
            Close(1125, map.HitObjects.OfType<ShredHitObject>().Single(n => n.IsLong).EndTime,
                "release leaves LN endpoint unchanged");
            KeyDown(c.StepRecorder, Key.S); KeyDown(c.StepRecorder, Key.K); KeyUp(c.StepRecorder, Key.S);
        };
        yield return () =>
        {
            Close(1375, clock.CurrentTimeAccurate, "held pick no longer delays final fret release");
            KeyUp(c.StepRecorder, Key.K);
        };
        yield return () =>
        {
            Close(1375, clock.CurrentTimeAccurate, "releasing fingerpick adds no extra step");
            c.StepRecorder.Enabled.Value = false;
        };
    }
}
