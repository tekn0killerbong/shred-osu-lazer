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

internal static class Revision51UiChecks
{
    const BindingFlags hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static object Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,hidden)!.Invoke(o,args)!;
    static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);Console.WriteLine("PASS v51 UI "+msg);}
    static InputState State(Vector2 p,Key? modifier=null){var s=new InputState();s.Mouse.Position=p;if(modifier.HasValue)s.Keyboard.Keys.SetPressed(modifier.Value,true);return s;}
    public static IEnumerable<Action> Steps(Editor editor)
    {
        var c=editor.ChildrenOfType<ShredHitObjectComposer>().Single();var b=(ShredBlueprintContainer)c.BlueprintContainer;
        var map=c.Dependencies.Get<EditorBeatmap>();var clock=c.Dependencies.Get<EditorClock>();var history=c.Dependencies.Get<IEditorChangeHandler>();
        void Undo()=>typeof(Editor).GetMethod("Undo",hidden)!.Invoke(editor,null);
        void Redo()=>typeof(Editor).GetMethod("Redo",hidden)!.Invoke(editor,null);
        void Click(Vector2 p,MouseButton button,Key? modifier=null)
        {
            Invoke(b,"OnMouseDown",new MouseDownEvent(State(p,modifier),button));
            Invoke(b,"OnMouseUp",new MouseUpEvent(State(p,modifier),button));
            Invoke(b,"OnClick",new ClickEvent(State(p,modifier),button));
        }
        yield return ()=>
        {
            c.StepRecorder.Enabled.Value=false;c.Playback.ReturnOnPause.Value=false;clock.Stop();c.SetSelectTool();map.TimelineZoom=0.5;
            map.BeginChange();foreach(var n in map.HitObjects.ToArray())map.Remove(n);
            for(int i=0;i<100;i++)map.Add(new ShredHitObject{StartTime=1000+i*10,Fret=i%5+1,Row=i%2});
            map.EndChange();clock.Seek(500);
        };
        yield return ()=>map.SelectedHitObjects.AddRange(map.HitObjects);
        yield return ()=>
        {
            Check(map.SelectedHitObjects.Count==100,"large group selected");
            Click(c.Grid.ToScreenSpace(new Vector2(5,5)),MouseButton.Left);
            Check(map.SelectedHitObjects.Count==0,"empty left click clears selection");
        };
        yield return ()=>{Undo();Check(map.SelectedHitObjects.Count==100,"menu Undo restores accidentally cleared selection");};
        yield return ()=>
        {
            Check(b.ChildrenOfType<ShredSelectionBlueprint>().Count(n=>n.IsSelected)==100,"all restored blueprints remain selected after update");
            Check(clock.CurrentTimeAccurate==500,"selection undo leaves grid stationary");Redo();Check(map.SelectedHitObjects.Count==0,"menu Redo clears selection again");
        };
        yield return ()=>{Undo();Check(map.SelectedHitObjects.Count==100,"second undo restores entire selection");Undo();Check(map.SelectedHitObjects.Count==0,"previous undo reverses select-all, keeping notes");Check(map.HitObjects.Count==100,"selection history never removes notes");};
        yield return ()=>{Redo();map.SelectedHitObjects.Clear();map.SelectedHitObjects.AddRange(map.HitObjects.Take(3));};
        yield return ()=>
        {
            Check(map.SelectedHitObjects.Count==3,"replace selected group");Undo();Check(map.SelectedHitObjects.Count==100,"replace selection undoes in one step");Redo();Check(map.SelectedHitObjects.Count==3,"replace selection redoes in one step");
            map.BeginChange();foreach(var n in map.SelectedHitObjects.ToArray()){n.StartTime+=125;map.Update(n);}map.EndChange();
        };
        yield return ()=>{Undo();Check(map.SelectedHitObjects.Count==3,"undo movement retains selection");};
        yield return ()=>{Redo();Check(map.SelectedHitObjects.Count==3,"redo movement retains selection");};
        foreach(bool recording in new[]{false,true})
        {
            yield return ()=>{c.StepRecorder.Enabled.Value=recording;map.SelectedHitObjects.Clear();map.SelectedHitObjects.AddRange(map.HitObjects.Take(8));};
            yield return ()=>{Click(c.Grid.ToScreenSpace(new Vector2(5,5)),MouseButton.Right);Check(map.SelectedHitObjects.Count==0,"empty right click clears group");};
            yield return ()=>{Undo();Check(map.SelectedHitObjects.Count==8,$"undo restores right-click deselection, recorder {recording}");};
        }
        yield return ()=>{c.StepRecorder.Enabled.Value=true;};
        yield return ()=>
        {
            Invoke(c.StepRecorder,"OnKeyDown",new KeyDownEvent(State(Vector2.Zero),Key.Escape));
            Check(map.SelectedHitObjects.Count==0,"Escape clears group");
        };
        yield return ()=>
        {
            Invoke(c.StepRecorder,"OnKeyDown",new KeyDownEvent(State(Vector2.Zero,Key.ControlLeft),Key.Z));
            Check(map.SelectedHitObjects.Count==8,"Ctrl+Z restores Escape-cleared selection");
        };
        yield return ()=>
        {
            Invoke(c.StepRecorder,"OnKeyDown",new KeyDownEvent(State(Vector2.Zero,Key.ControlLeft),Key.Y));
            Check(map.SelectedHitObjects.Count==0,"Ctrl+Y redoes Escape deselection");
        };
        yield return ()=>
        {
            c.StepRecorder.Enabled.Value=false;Undo();
            var selected=map.SelectedHitObjects.ToArray();b.ClipboardCommand(Key.D);
        };
        yield return ()=>
        {
            int after=map.HitObjects.Count;Undo();Check(map.HitObjects.Count<after,"one undo removes Ctrl+D copies without extra selection step");Redo();Check(map.HitObjects.Count==after,"one redo restores Ctrl+D copies");
        };
    }
}
