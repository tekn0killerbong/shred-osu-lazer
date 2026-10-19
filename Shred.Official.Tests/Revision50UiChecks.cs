using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Testing;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Skinning;
using osu.Game.Rulesets.Shred.UI;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;
using osuTK.Input;

internal static class Revision50UiChecks
{
    private const BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static object Invoke(object o, string name, params object[] args) => o.GetType().GetMethod(name,hidden)!.Invoke(o,args)!;
    private static InputState State(Vector2 p, bool ctrl=false)
    {
        var s=new InputState();s.Mouse.Position=p;if(ctrl)s.Keyboard.Keys.SetPressed(Key.ControlLeft,true);return s;
    }
    private static void Check(bool value, string label) { if(!value)throw new Exception(label); Console.WriteLine("PASS v50 UI " + label); }
    private static void Close(double expected, double actual, string label) => Check(Math.Abs(expected-actual)<0.01,$"{label}: {actual:0.###}");
    private static void Drag(ShredBlueprintContainer b, Vector2 p, Vector2 q, bool ctrl=false)
    {
        foreach(var blueprint in b.ChildrenOfType<ShredSelectionBlueprint>())
            typeof(Drawable).GetProperty("IsHovered")!.SetValue(blueprint,blueprint.ReceivePositionalInputAt(p));
        Invoke(b,"OnMouseDown",new MouseDownEvent(State(p,ctrl),MouseButton.Left));
        Check((bool)Invoke(b,"OnDragStart",new DragStartEvent(State(q,ctrl),MouseButton.Left,p)),"selection drag accepted");
        Invoke(b,"OnDrag",new DragEvent(State(q,ctrl),MouseButton.Left,p,p));
        Invoke(b,"OnDragEnd",new DragEndEvent(State(q,ctrl),MouseButton.Left,p));
        Invoke(b,"OnMouseUp",new MouseUpEvent(State(q,ctrl),MouseButton.Left));
    }
    public static IEnumerable<Action> Steps(Editor editor)
    {
        var c=editor.ChildrenOfType<ShredHitObjectComposer>().Single();
        var b=(ShredBlueprintContainer)c.BlueprintContainer;
        var map=c.Dependencies.Get<EditorBeatmap>();var clock=c.Dependencies.Get<EditorClock>();
        var divisor=c.Dependencies.Get<BindableBeatDivisor>();var history=c.Dependencies.Get<IEditorChangeHandler>();
        void Clear()
        {
            map.BeginChange();foreach(var n in map.HitObjects.ToArray())map.Remove(n);map.EndChange();
        }
        ShredHitObject[] notes=null;
        yield return () =>
        {
            c.StepRecorder.Enabled.Value=false;c.Playback.ReturnOnPause.Value=false;clock.Stop();
            Clear(); c.SetSelectTool();map.TimelineZoom=0.5;
            divisor.SetArbitraryDivisor(16,true);
            c.StepRecorder.ApplyGridCommand(ShredEditorAction.IncreaseGridType);
            Check(divisor.ValidDivisors.Value.Type==BeatDivisorType.Triplets && divisor.Value==6,"keyboard switching to Triplets starts at 1/6");
            divisor.Value=12;c.StepRecorder.ApplyGridCommand(ShredEditorAction.DecreaseGridType);
            Check(divisor.ValidDivisors.Value.Type==BeatDivisorType.Common && divisor.Value==4,"keyboard switching to Common starts at 1/4");
            divisor.ValidDivisors.Value=BeatDivisorPresetCollection.TRIPLETS;
        };
        yield return () =>
        {
            Check(divisor.Value==6,"sidebar switching to Triplets starts at 1/6");
            divisor.ValidDivisors.Value=BeatDivisorPresetCollection.COMMON;
        };
        yield return () =>
        {
            Check(divisor.Value==4,"sidebar switching to Common starts at 1/4");
            divisor.SetArbitraryDivisor(7);Check(divisor.Value==7,"custom grid remains usable");
            divisor.SetArbitraryDivisor(4,true);
            notes=new[]{new ShredHitObject{StartTime=1000,Fret=1},new ShredHitObject{StartTime=1000+500d/6,Fret=3},new ShredHitObject{StartTime=1000+500d/12,Row=1,Fret=4,Duration=500}};
            map.BeginChange();foreach(var n in notes)map.Add(n);map.EndChange();map.SelectedHitObjects.AddRange(notes);clock.Seek(750);
        };
        yield return () =>
        {
            var p=c.Grid.ToScreenSpace(c.Grid.PositionAt(notes[0].Column,1000));
            var q=c.Grid.ToScreenSpace(c.Grid.PositionAt(notes[0].Column,1125));Drag(b,p,q);
            Close(1125,notes[0].StartTime,"drag anchor by one 1/4 step");
            Close(1125+500d/6,notes[1].StartTime,"drag preserves original 1/6 offset");
            Close(1125+500d/12,notes[2].StartTime,"drag preserves other row's 1/12 offset");
            Close(500,notes[2].Duration,"drag preserves LN duration");
            Check(map.SelectedHitObjects.Count==3,"drag retains selection");history.RestoreState(-1);
        };
        yield return () =>
        {
            Check(map.SelectedHitObjects.Count==3,"undo drag restores all selected blueprints");
            Check(b.ChildrenOfType<ShredSelectionBlueprint>().Count(n=>n.IsSelected)==3,"selection survives blueprint rebuild after undo");
            history.RestoreState(1);
        };
        yield return () =>
        {
            Check(map.SelectedHitObjects.Count==3,"redo drag restores selection");
            notes=map.HitObjects.Cast<ShredHitObject>().OrderBy(n=>n.Column).ToArray();
            var first=notes.Single(n=>n.Fret==1);var p=c.Grid.ToScreenSpace(c.Grid.PositionAt(first.Column,first.StartTime));
            var q=c.Grid.ToScreenSpace(c.Grid.PositionAt(first.Column,first.StartTime+500));
            Drag(b,p,q,true);
            Check(map.HitObjects.Count==6 && map.SelectedHitObjects.Count==3,"Ctrl+drag copies and selects three notes");
            var copied=map.SelectedHitObjects.Cast<ShredHitObject>().OrderBy(n=>n.Column).ToArray();
            foreach(var original in notes) Close(original.StartTime+500,copied.Single(n=>n.Column==original.Column).StartTime,"Ctrl+drag preserves mixed-grid offsets");
        };
        foreach(int spacing in new[]{1,4,6,12,16,64})
        {
            ShredHitObject ln=null;
            yield return () =>
            {
                Clear();divisor.SetArbitraryDivisor(spacing);clock.Seek(1000);
                ln=new ShredHitObject{StartTime=1000,Fret=2,Duration=2000,Articulation=ShredPracticeArticulation.HammerPull};
                map.BeginChange();map.Add(ln);map.EndChange();
            };
            yield return () =>
            {
                var drawable=c.ChildrenOfType<DrawableShredEditorNote>().Single(n=>n.HitObject==ln);
                var visual=drawable.ChildrenOfType<ShredNoteVisual>().Single();
                var head=(Drawable)typeof(ShredNoteVisual).GetField("head",hidden)!.GetValue(visual)!;
                Check(head.Alpha==1,$"LN head visible at real base on 1/{spacing}");
                var halo=(Drawable)typeof(ShredNoteVisual).GetField("halo",hidden)!.GetValue(visual)!;
                var skin=(ShredSkinSettings)typeof(ShredNoteVisual).GetField("skin",hidden)!.GetValue(visual)!;
                Close(skin.HaloOpacity,halo.Alpha,"HP halo uses its own skin opacity");
                var outline=(Drawable)typeof(ShredNoteVisual).GetField("haloOutline",hidden)!.GetValue(visual)!;
                Check(outline.Alpha>0 && outline.Alpha<halo.Alpha/4,"HP outline adds a small boost without doubling the glow");
                var glow=outline.ChildrenOfType<osu.Framework.Graphics.Containers.Container>().First(n=>n.Masking).EdgeEffect.Colour.SRGB;
                Check(glow.R>glow.G && glow.G>glow.B,"default HP halo is golden");
                clock.Seek(c.Grid.NextStep(1000));
            };
            yield return () =>
            {
                var drawable=c.ChildrenOfType<DrawableShredEditorNote>().Single(n=>n.HitObject==ln);
                var visual=drawable.ChildrenOfType<ShredNoteVisual>().Single();
                var head=(Drawable)typeof(ShredNoteVisual).GetField("head",hidden)!.GetValue(visual)!;
                Check(head.Alpha==0,$"LN head hidden at next 1/{spacing} step, even within receptor height");
                var body=(Drawable)typeof(ShredNoteVisual).GetField("body",hidden)!.GetValue(visual)!;
                Check(body.Alpha==1 && drawable.Alpha==1,"extension remains visible after head disappears");
            };
        }
        foreach(bool recording in new[]{false,true})
        foreach(bool preference in new[]{false,true})
        foreach(bool ctrl in new[]{false,true})
        {
            double stopped=0;
            void Space(bool modified)
            {
                var s=State(Vector2.Zero,modified);s.Keyboard.Keys.SetPressed(Key.Space,true);
                Check((bool)Invoke(c.StepRecorder,"OnKeyDown",new KeyDownEvent(s,Key.Space)),"Space handled independently of recorder focus/state");
                Invoke(c.StepRecorder,"OnKeyUp",new KeyUpEvent(State(Vector2.Zero),Key.Space));
            }
            yield return () =>
            {
                clock.Stop();c.StepRecorder.Enabled.Value=recording;c.Playback.ReturnOnPause.Value=preference;
                clock.Seek(1000);Space(false);
            };
            yield return () =>
            {
                Check(clock.IsRunning,"playback starts");stopped=clock.CurrentTimeAccurate;
                Check(stopped>1100,"playback advances before pause");Space(ctrl);
                Check(!clock.IsRunning,"playback stops");
                if(preference!=ctrl) Close(1000,clock.CurrentTimeAccurate,$"return: recorder={recording}, preference={preference}, Ctrl={ctrl}");
                else Check(clock.CurrentTimeAccurate>=stopped-1,$"stay: recorder={recording}, preference={preference}, Ctrl={ctrl}");
            };
            yield return () =>
            {
                if(preference!=ctrl) Close(1000,clock.CurrentTimeAccurate,"return remains after next UI update");
                else Check(clock.CurrentTimeAccurate>=stopped-1,"pause observer does not override Ctrl+Space");
            };
        }
    }
}
