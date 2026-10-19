using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Testing;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Input;

internal static class Revision41UiChecks
{
    private static object Invoke(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(o, args)!;
    private static InputState State(Vector2 p) { var state = new InputState(); state.Mouse.Position = p; return state; }
    private static void Drag(ShredBlueprintContainer b, Vector2 p, Vector2 q)
    {
        // Direct event probes must supply the hover state normally set by InputManager.
        foreach (var blueprint in b.ChildrenOfType<ShredSelectionBlueprint>())
            typeof(Drawable).GetProperty("IsHovered")!.SetValue(blueprint, blueprint.ReceivePositionalInputAt(p));
        Invoke(b, "OnMouseDown", new MouseDownEvent(State(p), MouseButton.Left));
        Revision39Checks.Check((bool)Invoke(b, "OnDragStart", new DragStartEvent(State(q), MouseButton.Left, p)), "v41 LN handle drag accepted");
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
        var a = new ShredHitObject { StartTime = 1000, Fret = 2, Duration = 1000 };
        var other = new ShredHitObject { StartTime = 1250, Row = 1, Fret = 4, Duration = 1250 };
        var shortNote = new ShredHitObject { StartTime = 1500, Fret = 4 };
        var unselected = new ShredHitObject { StartTime = 1000, Row = 1, Fret = 1, Duration = 1000 };
        yield return () =>
        {
            c.StepRecorder.Enabled.Value = false;
            map.BeginChange();
            foreach (var note in map.HitObjects.ToArray()) map.Remove(note);
            foreach (var note in new[] { a, other, shortNote, unselected }) map.Add(note);
            map.EndChange();
            clock.Seek(0); map.TimelineZoom = 0.5; divisor.Value = 2; c.SetSelectTool();
            map.SelectedHitObjects.AddRange(new[] { a, other, shortNote });
        };
        yield return () =>
        {
            var p = c.Grid.ToScreenSpace(c.Grid.PositionAt(a.Column, a.StartTime));
            var q = c.Grid.ToScreenSpace(c.Grid.PositionAt(a.Column, a.StartTime + 250));
            Drag(b, p, q);
            Revision39Checks.Check(a.StartTime == 1250 && other.StartTime == 1500 && a.EndTime == 2250 && other.EndTime == 2750,
                "v43 dragging an LN head moves the selection and preserves LN durations");
            Revision39Checks.Check(shortNote.StartTime == 1750 && unselected.StartTime == 1000 && unselected.Duration == 1000,
                "v43 selected short notes move too; unselected LNs remain unchanged");
        };
        yield return () => { clock.Seek(1250); };
        yield return () =>
        {
            // Choose the incoming edge inside the tail's one-head-long hit area.
            var p = c.Grid.ToScreenSpace(c.Grid.PositionAt(other.Column, other.EndTime));
            var q = c.Grid.ToScreenSpace(c.Grid.PositionAt(other.Column, other.EndTime + 250));
            Revision39Checks.Check(c.Grid.EndHandleContains(other, p), "v41 tail handle is visible and hittable");
            Drag(b, p, q);
            Revision39Checks.Check(a.EndTime == 2500 && other.EndTime == 3000 && a.StartTime == 1250 && other.StartTime == 1500,
                "v41 selected LN ends resize together, keeping their starts");
        };
        foreach (bool recording in new[] { false, true })
        {
            yield return () => { c.StepRecorder.Enabled.Value = recording; clock.Seek(500); };
            yield return () =>
            {
                map.SelectedHitObjects.Clear(); map.SelectedHitObjects.Add(a);
                var empty = c.Grid.ToScreenSpace(new Vector2(5, 5));
                double before = clock.CurrentTimeAccurate;
                Invoke(b, "OnMouseDown", new MouseDownEvent(State(empty), MouseButton.Left));
                Invoke(b, "OnClick", new ClickEvent(State(empty), MouseButton.Left));
                Revision39Checks.Check(map.SelectedHitObjects.Count == 0 && clock.CurrentTimeAccurate == before,
                    $"v41 empty click clears selection without seeking (recorder {recording})");
            };
        }
        yield return () =>
        {
            c.StepRecorder.Enabled.Value = false;
            var history = c.Dependencies.Get<IEditorChangeHandler>();
            history.RestoreState(-1);
            var holds = map.HitObjects.OfType<ShredHitObject>().ToArray();
            Revision39Checks.Check(holds.Single(n => n.Row == 0 && n.Fret == 2).EndTime == 2250
                && holds.Single(n => n.Row == 1 && n.Fret == 4).EndTime == 2750,
                "v41 one undo restores both resized LN ends");
            history.RestoreState(1);
            holds = map.HitObjects.OfType<ShredHitObject>().ToArray();
            Revision39Checks.Check(holds.Single(n => n.Row == 0 && n.Fret == 2).EndTime == 2500
                && holds.Single(n => n.Row == 1 && n.Fret == 4).EndTime == 3000,
                "v41 one redo reapplies both resized LN ends");
        };
        ShredHitObject body = null, under = null, shortAtTail = null;
        yield return () =>
        {
            map.BeginChange();
            foreach(var n in map.HitObjects.ToArray()) map.Remove(n);
            body=new ShredHitObject {StartTime=1000,Duration=1000,Fret=2};
            map.Add(body);map.EndChange();
            map.SelectedHitObjects.Clear();map.SelectedHitObjects.Add(body);clock.Seek(750);
        };
        yield return () =>
        {
            var p=c.Grid.ToScreenSpace(c.Grid.PositionAt(body.Column,1500));
            var q=c.Grid.ToScreenSpace(c.Grid.PositionAt(body.Column,1750));
            Revision39Checks.Check(!c.Grid.EndHandleContains(body,p), "v43 sustain body is outside tail handle");
            Drag(b,p,q);
            Revision39Checks.Check(body.StartTime==1250 && body.Duration==1000, "v43 dragging sustain body moves note without resizing");
        };
        yield return () =>
        {
            map.BeginChange();
            foreach(var n in map.HitObjects.ToArray()) map.Remove(n);
            under=new ShredHitObject {StartTime=1000,Duration=1500,Row=1,Fret=3};
            shortAtTail=new ShredHitObject {StartTime=2500,Row=1,Fret=3};
            map.Add(under);map.Add(shortAtTail);map.EndChange();
            map.SelectedHitObjects.Clear();map.SelectedHitObjects.Add(under);clock.Seek(1500);
        };
        yield return () =>
        {
            var pad=new Vector2(c.Grid.NoteSize(under.Fret).X/2+2*c.Grid.Unit,0);
            var p=c.Grid.ToScreenSpace(c.Grid.PositionAt(under.Column,under.EndTime)+pad);
            var q=c.Grid.ToScreenSpace(c.Grid.PositionAt(under.Column,under.EndTime-250)+pad);
            Revision39Checks.Check(!c.Grid.NoteContains(shortAtTail,p) && c.Grid.EndHandleContains(under,p), "v43 widened tail handle stays reachable beside an overlapping short note");
            Drag(b,p,q);
            Revision39Checks.Check(under.StartTime==1000 && under.EndTime==2250 && shortAtTail.StartTime==2500, "v43 widened tail drag resizes only the selected LN");
        };
        yield return () =>
        {
            var tiny=new ShredHitObject {StartTime=2000,Duration=1,Fret=2};
            var head=c.Grid.ToScreenSpace(c.Grid.PositionAt(tiny.Column,tiny.StartTime));
            var side=c.Grid.ToScreenSpace(c.Grid.PositionAt(tiny.Column,tiny.EndTime)+new Vector2(c.Grid.NoteSize(2).X/2+2*c.Grid.Unit,0));
            Revision39Checks.Check(!c.Grid.EndHandleContains(tiny,head) && c.Grid.EndHandleContains(tiny,side), "v43 very short LN retains movable head and accessible tail wings");
        };
    }
}
