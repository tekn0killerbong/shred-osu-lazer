using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Compatibility;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;

internal static class Revision50HistoryChecks
{
    public static void Run()
    {
        using var store = new Store(new ShredRuleset().RulesetInfo);
        global::osu.Game.Beatmaps.Formats.Decoder.RegisterDependencies(store);
        var source = new Beatmap<ShredHitObject>(); source.BeatmapInfo.Ruleset = new ShredRuleset().RulesetInfo;
        using var map = new EditorBeatmap(source);
        using var history = new BeatmapEditorChangeHandler(map);
        using var selections = new ShredEditorHistory(map, history);
        void Check(bool ok, string msg) { if (!ok) throw new Exception(msg); Console.WriteLine("PASS v50 " + msg); }
        ShredHitObject[] Notes() => map.HitObjects.Cast<ShredHitObject>().ToArray();
        void CheckSelected(int count, string msg) => Check(map.SelectedHitObjects.Count == count && map.SelectedHitObjects.All(map.HitObjects.Contains), msg);
        map.BeginChange();
        map.Add(new ShredHitObject { StartTime=1000, Fret=1 });
        map.Add(new ShredHitObject { StartTime=1083, Fret=3, Duration=500 });
        map.Add(new ShredHitObject { StartTime=1400, Row=1, Fret=5 });
        map.EndChange();
        map.SelectedHitObjects.AddRange(Notes().Take(2));
        map.BeginChange();
        foreach(var n in map.SelectedHitObjects.Cast<ShredHitObject>().ToArray()) { n.StartTime += 125; map.Update(n); }
        map.EndChange();
        history.RestoreState(-1); CheckSelected(2, "undo movement restores selected notes after object replacement");
        Check(Notes()[0].StartTime==1000 && Notes()[1].StartTime==1083, "undo restores mixed-grid times");
        history.RestoreState(1); CheckSelected(2, "redo movement retains selection");
        Check(Notes()[0].StartTime==1125 && Notes()[1].StartTime==1208, "redo restores shared movement");
        map.BeginChange();
        foreach(var n in map.SelectedHitObjects.ToArray()) map.Remove(n);
        map.EndChange();
        history.RestoreState(-1); CheckSelected(2,"undo deletion restores selected objects");
        history.RestoreState(1); CheckSelected(0,"redo deletion clears only deleted selection");
        history.RestoreState(-1); CheckSelected(2,"repeated undo deletion restores same selection");
        map.BeginChange();
        foreach(var n in map.SelectedHitObjects.Cast<ShredHitObject>().ToArray()) { n.Duration += 100; map.Update(n); }
        map.EndChange();
        Check(!history.CanRedo.Value,"new branch discards stale redo selection");
        history.RestoreState(-1); CheckSelected(2,"undo group resize restores selection");
        history.RestoreState(1); CheckSelected(2,"redo group resize restores selection");
        foreach (var n in Notes()) { n.Samples=new List<HitSampleInfo>{new HitSampleInfo(HitSampleInfo.HIT_CLAP,HitSampleInfo.BANK_NORMAL)}; map.Update(n); }
        map.SaveState();
        history.RestoreState(-1); CheckSelected(2,"undo samples saved without explicit transaction restores selection");
        history.RestoreState(1); CheckSelected(2,"redo samples preserves selection");
        map.BeginChange();
        var pasted=new ShredHitObject { StartTime=3000,Fret=2 };map.Add(pasted);
        map.EndChange();
        // Paste selects its new blueprints on the next update.
        map.SelectedHitObjects.Clear();map.SelectedHitObjects.Add(pasted);
        history.RestoreState(-1);CheckSelected(2,"undo paste restores prior selection");
        history.RestoreState(1);CheckSelected(1,"redo paste restores deferred selection");
        Check(map.SelectedHitObjects.Single().StartTime==3000,"redo selects pasted note rather than same old list index");
        for(int i=0;i<60;i++)
        {
            map.BeginChange();var n=Notes().Last();n.StartTime+=125;map.Update(n);map.EndChange();
        }
        int undos=0;
        while(history.CanUndo.Value && undos++<100) { history.RestoreState(-1);CheckSelected(1,"selection survives bounded history undo " + undos); }
        Check(undos==50,"selection history reaches native oldest retained state");
        int redos=0;
        while(history.CanRedo.Value && redos++<100) { history.RestoreState(1);CheckSelected(1,"selection survives bounded history redo " + redos); }
        Check(redos==undos,"entire bounded selection history redoes");
    }
}
