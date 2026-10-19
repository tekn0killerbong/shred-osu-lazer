using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Compatibility;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;

internal static class Revision51HistoryChecks
{
    public static void Run()
    {
        using var store=new Store(new ShredRuleset().RulesetInfo);
        global::osu.Game.Beatmaps.Formats.Decoder.RegisterDependencies(store);
        var source=new Beatmap<ShredHitObject>();source.BeatmapInfo.Ruleset=new ShredRuleset().RulesetInfo;
        using var map=new EditorBeatmap(source);
        using var native=new BeatmapEditorChangeHandler(map);
        using var history=new ShredEditorHistory(map,native);
        void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS v51 "+label);}
        string Geometry()=>string.Join(";",map.HitObjects.Cast<ShredHitObject>().Select(n=>$"{n.StartTime:R}/{n.Duration:R}/{n.Column}/{n.Articulation}"));
        void Select(params int[] indices)
        {
            map.SelectedHitObjects.Clear();map.SelectedHitObjects.AddRange(indices.Select(i=>map.HitObjects[i]));history.FinishFrame();
        }
        void Selected(params int[] indices)=>Check(map.SelectedHitObjects.ToHashSet().SetEquals(indices.Select(i=>map.HitObjects[i])),"selection: "+indices.Length+" expected notes");
        map.BeginChange();
        for(int i=0;i<1000;i++)map.Add(new ShredHitObject{StartTime=1000+i*10.125,Fret=i%5+1,Row=i%2,Duration=i%3==0?4.125:0});
        map.EndChange();history.FinishFrame();
        string content=native.CurrentStateHash,geometry=Geometry();
        Select(Enumerable.Range(0,1000).ToArray());Selected(Enumerable.Range(0,1000).ToArray());
        Check(native.CurrentStateHash==content,"select-all does not dirty beatmap hash");
        Select();Selected();
        native.RestoreState(-1);Check(map.SelectedHitObjects.Count==1000,"one native undo restores accidentally deselected large group");
        Check(Geometry()==geometry && native.CurrentStateHash==content,"selection-only undo preserves exact fractional times, durations and serialized beatmap");
        native.RestoreState(1);Selected();
        native.RestoreState(-1);Check(map.SelectedHitObjects.Count==1000,"undo clearing remains repeatable");
        native.RestoreState(-1);Selected();
        Check(map.HitObjects.Count==1000,"undo selection keeps every note");
        native.RestoreState(1);Check(map.SelectedHitObjects.Count==1000,"redo reselects all in one step");
        Select(2,5,8);Selected(2,5,8);
        native.RestoreState(-1);Check(map.SelectedHitObjects.Count==1000,"replace selection is one action, not clear plus add actions");
        native.RestoreState(1);Selected(2,5,8);
        map.BeginChange();foreach(var n in map.SelectedHitObjects.ToArray()){n.StartTime+=125;map.Update(n);}map.EndChange();history.FinishFrame();
        string moved=Geometry();native.RestoreState(-1);
        Check(map.SelectedHitObjects.Count==3,"undo move preserves selected group");
        native.RestoreState(-1);Check(map.SelectedHitObjects.Count==1000,"next undo reverses the preceding selection change");
        native.RestoreState(1);native.RestoreState(1);Check(map.SelectedHitObjects.Count==3,"redo selection then move preserves group");
        Select();native.RestoreState(-1);Check(map.SelectedHitObjects.Count==3,"undo accidental deselect after movement restores moved group");
        Check(Geometry()==moved,"restoring selection does not undo movement");
        // A drag spanning multiple update frames is a single history entry.
        history.FinishFrame();
        map.SelectedHitObjects.Clear();
        for(int i=0;i<20;i++){map.SelectedHitObjects.Add(map.HitObjects[i]);history.FinishFrame(mouseHeld:true);}
        history.FinishFrame();native.RestoreState(-1);Check(map.SelectedHitObjects.Count==3,"twenty-frame rectangle selection undoes as one gesture");
        native.RestoreState(1);Check(map.SelectedHitObjects.Count==20,"rectangle selection redoes in one gesture");
        native.RestoreState(-1);Check(native.CanRedo.Value,"redo available before no-op selection");
        var old=map.SelectedHitObjects.ToArray();map.SelectedHitObjects.Clear();map.SelectedHitObjects.AddRange(old);history.FinishFrame();
        Check(native.CanRedo.Value,"no-op selection keeps redo branch");native.RestoreState(1);Check(map.SelectedHitObjects.Count==20,"redo branch still restores original group");
        native.RestoreState(-1);Select(77);Check(!native.CanRedo.Value,"new real selection discards old redo branch");
        map.BeginChange();foreach(var n in map.SelectedHitObjects.ToArray())map.Remove(n);map.EndChange();history.FinishFrame();
        native.RestoreState(-1);Check(map.SelectedHitObjects.Count==1 && map.HitObjects.Count==1000,"undo deletion restores notes plus selection without intermediate selection step");
        native.RestoreState(1);Check(map.SelectedHitObjects.Count==0 && map.HitObjects.Count==999,"redo deletion remains one step");
        native.RestoreState(-1);history.FinishFrame();
        // Deferred selection belongs to paste's edit, not to a second undo step.
        var pasted=new ShredHitObject{StartTime=50000,Fret=2};map.BeginChange();map.Add(pasted);map.EndChange();
        map.SelectedHitObjects.Clear();map.SelectedHitObjects.Add(pasted);history.FinishFrame();
        native.RestoreState(-1);Check(map.HitObjects.Count==1000 && map.SelectedHitObjects.Count==1,"one undo of paste removes pasted note and restores prior selection");
        native.RestoreState(1);Check(map.HitObjects.Count==1001 && map.SelectedHitObjects.Single().StartTime==50000,"redo paste restores deferred copied selection");
        for(int i=0;i<60;i++)Select(i%2);
        var retained=map.SelectedHitObjects.ToArray();
        map.SelectedHitObjects.Clear();map.SelectedHitObjects.AddRange(retained);history.FinishFrame();
        int undos=0;while(native.CanUndo.Value && undos++<100)native.RestoreState(-1);
        Check(undos==50 && map.SelectedHitObjects.Count==1,"no-op at history capacity keeps all fifty undo actions");
        int redos=0;while(native.CanRedo.Value && redos++<100)native.RestoreState(1);
        Check(redos==50 && map.SelectedHitObjects.Count==1,"all fifty selection actions redo correctly");
    }
}
