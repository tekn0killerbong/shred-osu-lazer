using System.Reflection;
using osu.Framework.Allocation;
using MouseState=osu.Framework.Input.States.MouseState;
using KeyboardState=osu.Framework.Input.States.KeyboardState;
using osu.Framework.Testing;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Graphics.UserInterface;
using osuTK;
using osuTK.Input;

internal static class Revision39UiChecks
{
    private static object Invoke(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(o,args)!;
    private static InputState State(Vector2 p, bool ctrl=false, Key? key=null)
    {
        var state=new InputState();state.Mouse.Position=p;
        if(ctrl)state.Keyboard.Keys.SetPressed(Key.ControlLeft,true);
        if(key.HasValue)state.Keyboard.Keys.SetPressed(key.Value,true);
        return state;
    }
    public static IEnumerable<Action> Steps(Editor editor)
    {
        var c=editor.ChildrenOfType<ShredHitObjectComposer>().Single();
        var b=(ShredBlueprintContainer)c.BlueprintContainer;
        var map=c.Dependencies.Get<EditorBeatmap>();
        var clock=c.Dependencies.Get<EditorClock>();
        var divisor=c.Dependencies.Get<BindableBeatDivisor>();
        ShredHitObject a=new() { StartTime=1000, Fret=2, Duration=100 }, other=new() {StartTime=1166.666, Fret=3, Row=1, Duration=77};
        yield return () => {
            c.StepRecorder.Enabled.Value=false;
            c.Playback.ReturnOnPause.Value=false;
            map.BeginChange(); foreach(var n in map.HitObjects.ToArray())map.Remove(n); map.Add(a);map.Add(other);map.EndChange();
            clock.Seek(0);map.TimelineZoom=0.25;divisor.Value=2;c.SetSelectTool();
        };
        yield return () => {
            Revision39Checks.Check(!c.ChildrenOfType<ShredStepRecorderToggle>().Single().ChildrenOfType<OsuNumberBox>().Any(),
                "LN threshold box removed from recorder sidebar");
            map.SelectedHitObjects.AddRange(new[]{a,other});b.ClipboardCommand(Key.C);
            clock.Seek(3000);c.Grid.FocusRow(1);b.ClipboardCommand(Key.V);
        };
        yield return () => {
            var pasted=map.HitObjects.Cast<ShredHitObject>().Where(n=>n.StartTime>2000).OrderBy(n=>n.StartTime).ToArray();
            Revision39Checks.Check(pasted.Length==2 && Math.Abs(pasted[1].StartTime-pasted[0].StartTime-166.666)<0.001 && pasted[0].Duration==100 && pasted[1].Duration==77 && pasted[0].Row==0 && pasted[1].Row==1,"paste preserves off-grid intervals, LN lengths and both original rows");
            map.SelectedHitObjects.Clear();map.SelectedHitObjects.Add(a);
            double expected=a.EndTime;b.ClipboardCommand(Key.D);
            Revision39Checks.Check(map.HitObjects.Any(n=>Math.Abs(n.StartTime-expected)<0.001),"Ctrl+D begins exactly at the selected LN end");
            // At this zoom the duplicate overlaps the original's hit area.
            // Remove clipboard test objects before testing an unambiguous click.
            map.BeginChange();
            foreach (var n in map.HitObjects.Where(n => n != a && n != other).ToArray()) map.Remove(n);
            map.EndChange();
            clock.Seek(0);
        };
        yield return () => {
            map.SelectedHitObjects.Clear();map.SelectedHitObjects.AddRange(new[]{a,other});
        };
        // Selection blueprints synchronise their IsSelected state on update.
        yield return () => {
            var p=c.Grid.ToScreenSpace(c.Grid.PositionAt(a.Column,a.StartTime));
            clock.Seek(0);
            p=c.Grid.ToScreenSpace(c.Grid.PositionAt(a.Column,a.StartTime));
            Console.WriteLine($"UI geometry {p} inside {c.Grid.ContainsScreen(p)} alive {b.ChildrenOfType<ShredSelectionBlueprint>().Count()} time {clock.CurrentTime}");
            Invoke(b,"OnMouseDown",new MouseDownEvent(State(p),MouseButton.Left));
            Invoke(b,"OnClick",new ClickEvent(State(p),MouseButton.Left));
            Revision39Checks.Check(map.SelectedHitObjects.Count==1 && map.SelectedHitObjects[0]==a,"plain click replaces multiselection with clicked note");
            var empty=c.Grid.ToScreenSpace(new Vector2(5,5));
            Invoke(b,"OnClick",new ClickEvent(State(empty),MouseButton.Left));
            Revision39Checks.Check(map.SelectedHitObjects.Count==0,"empty left click clears selection");
            Invoke(b,"OnMouseDown",new MouseDownEvent(State(empty),MouseButton.Right));
            Revision39Checks.Check(map.SelectedHitObjects.Count==0,"empty right click clears selection");
            clock.Seek(0);
        };
        yield return () => {
            map.SelectedHitObjects.Clear();map.SelectedHitObjects.AddRange(new[]{a,other});
        };
        // Selection blueprints synchronise their IsSelected state on update.
        yield return () => {
            var p=c.Grid.ToScreenSpace(c.Grid.PositionAt(a.Column,a.StartTime));
            var q=c.Grid.ToScreenSpace(c.Grid.PositionAt(a.Column,a.StartTime+500));
            int count=map.HitObjects.Count;
            Invoke(b,"OnMouseDown",new MouseDownEvent(State(p,true),MouseButton.Left));
            Invoke(b,"OnDragStart",new DragStartEvent(State(q,true),MouseButton.Left,p));
            Invoke(b,"OnDragEnd",new DragEndEvent(State(q,true),MouseButton.Left,p));
            Revision39Checks.Check(map.HitObjects.Count==count+2 && a.StartTime==1000 && other.StartTime==1166.666,"Ctrl+drag duplicates a selection without moving originals");
            var copied=map.SelectedHitObjects.OrderBy(n=>n.StartTime).ToArray();
            Revision39Checks.Check(copied.Length==2 && Math.Abs(copied[1].StartTime-copied[0].StartTime-166.666)<0.001,"Ctrl+drag preserves internal timing");
            c.StepRecorder.Enabled.Value=true;
        };
        yield return () => {
            bool handled=(bool)Invoke(c.StepRecorder,"OnKeyDown",new KeyDownEvent(State(Vector2.Zero,key:Key.Escape),Key.Escape));
            Revision39Checks.Check(handled && map.SelectedHitObjects.Count==0,"Escape clears selection first");
            handled=(bool)Invoke(c.StepRecorder,"OnKeyDown",new KeyDownEvent(State(Vector2.Zero,key:Key.Escape),Key.Escape));
            Revision39Checks.Check(!handled,"Escape without selection reaches native editor exit");
            Revision39Checks.Check(c.StepRecorder.Enabled.Value,"Escape does not disable recorder");
            c.StepRecorder.Enabled.Value=false;
            clock.Seek(0);clock.SeekSmoothlyTo(500);
            Revision39Checks.Check(clock.CurrentTimeAccurate==500 && clock.CurrentTime<500,"native smooth seek retains exact destination during animation");
        };
    }
}
