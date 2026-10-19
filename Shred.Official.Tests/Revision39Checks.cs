using System.Reflection;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Compatibility;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Ranking.Statistics;

internal static class Revision39Checks
{
    public static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS v39 " + label); }
    public static void Run()
    {
        var map = new Beatmap<ShredHitObject>();
        map.BeatmapInfo.Ruleset = new ShredRuleset().RulesetInfo;
        using var editor = new EditorBeatmap(map);
        using var history = new BeatmapEditorChangeHandler(editor);
        using var selection = new ShredEditorHistory(editor, history);
        var n = new ShredHitObject { StartTime = 1000, Fret = 1 };
        editor.BeginChange(); editor.Add(n); editor.EndChange();
        editor.SelectedHitObjects.Add(n);
        editor.BeginChange(); editor.Remove(n); editor.EndChange();
        Check(editor.HitObjects.Count == 0, "delete returns to identical empty content");
        history.RestoreState(-1);
        Check(editor.HitObjects.Count == 1 && editor.SelectedHitObjects.Count == 1, "undo deletion restores note and selection");
        history.RestoreState(1);
        Check(editor.HitObjects.Count == 0 && editor.SelectedHitObjects.Count == 0, "redo deletion removes note and selection");
        var a = new ShredHitObject { StartTime=1000, Duration=1000, Fret=1 };
        var neighbour = new ShredHitObject { StartTime=1000, Duration=2000, Fret=2 };
        var b = new ShredHitObject { StartTime=1500, Fret=1 };
        ShredEditorOperations.ResolveTailConflicts(new[]{a,neighbour,b}, t=>t-250, _=>{},new[]{b});
        Check(a.EndTime==1500 && neighbour.EndTime==3000,"mouse tail cut is exact and neighbouring LN stays unchanged");
        ShredEditorOperations.ResolveTailConflicts(new[]{a,neighbour,b}, t=>t-250, _=>{},new[]{b});
        Check(a.EndTime==1500,"repeated tail resolution is stable at touching endpoint");
        a.ApplyDefaults(map.ControlPointInfo,map.Difficulty);
        b.ApplyDefaults(map.ControlPointInfo,map.Difficulty);
        var events = new[]{new HitEvent(0,1,HitResult.Great,a,null,null),new HitEvent(12,1,HitResult.Great,a.NestedHitObjects.Single(),null,null),new HitEvent(-15,1,HitResult.Great,b,null,null)};
        using var graph=new HitEventTimingDistributionGraph(events);
        var retained=(IReadOnlyList<HitEvent>)typeof(HitEventTimingDistributionGraph).GetField("hitEvents",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(graph)!;
        Check(retained.Count==2 && retained.All(e=>e.HitObject!=a),"official timing distribution excludes LN tails, includes heads and short notes");
        Check(a.TimingWindows.ResultFor(0)==HitResult.Great && a.TimingWindows.ResultForEarlyRelease(-200)==HitResult.Great,"tail graph exclusion preserves hold timing windows");
        using var storage=new osu.Framework.Testing.TemporaryNativeStorage("shred-v39-config");
        using var config=new ShredRecorderConfig(storage);
        Check(!config.Get<bool>(ShredRecorderSetting.ReturnOnPause),"new return-on-pause default is off");
        using var playback=new ShredEditorPlayback();
        playback.ReturnOnPause.Value=true;
        Check((bool)typeof(ShredEditorPlayback).GetProperty("shouldReturn",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(playback)!,"return-on-pause stays active with recorder off");
    }
}
