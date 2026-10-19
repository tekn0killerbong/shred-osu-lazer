using System.Reflection;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Play;

internal static class RecorderTailChecks
{
    private const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run()
    {
        foreach (double at in new[] { 750d, 1500d, 2000d, 2250d })
        {
            var ruleset = new ShredRuleset();
            var map = new Beatmap<ShredHitObject>();
            map.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
            var tail = new ShredHitObject { StartTime = 1000, Duration = 1000, Fret = 1 };
            // This neighbouring LN used to trigger the one-step retreat at commit.
            map.HitObjects.Add(tail);
            map.HitObjects.Add(new ShredHitObject { StartTime = 1000, Duration = 2000, Fret = 2 });
            map.HitObjects.Add(new ShredHitObject { StartTime = 1000, Duration = 2000, Row = 1, Fret = 1 });
            using var editor = new EditorBeatmap(map);
            using var history = new BeatmapEditorChangeHandler(editor);
            using var composer = new ShredHitObjectComposer(ruleset);
            typeof(HitObjectComposer<ShredHitObject, ShredEditorAction>).GetProperty("EditorBeatmap", hidden)!.SetValue(composer, editor);
            using var recorder = new ShredStepRecorder(ruleset.RulesetInfo);
            using var clock = new EditorClock();
            using var underlying = new FramedBeatmapClock(false, false, new Clock());
            typeof(EditorClock).GetField("underlyingClock", hidden)!.SetValue(clock, underlying);
            typeof(ShredStepRecorder).GetProperty("beatmap", hidden)!.SetValue(recorder, editor);
            typeof(ShredStepRecorder).GetProperty("composer", hidden)!.SetValue(recorder, composer);
            typeof(ShredStepRecorder).GetProperty("editorClock", hidden)!.SetValue(recorder, clock);
            var state = new ShredStepRecorderState(at, t => t + 125,
                n => typeof(ShredStepRecorder).GetMethod("addNote", hidden)!.Invoke(recorder, new object[] { n }),
                _ => { }, _ => { });
            typeof(ShredStepRecorder).GetField("recorder", hidden)!.SetValue(recorder, state);
            state.Press(1, true);
            state.Release(1);
            typeof(ShredStepRecorder).GetMethod("endChange", hidden)!.Invoke(recorder, null);
            // The harness does not load a visual playfield; detach it for disposal.
            typeof(ShredStepRecorder).GetProperty("composer", hidden)!.SetValue(recorder, null);
            double expected = at > 1000 && at < 2000 ? at : 2000;
            if (tail.EndTime != expected || map.HitObjects.Where(n => n.Fret != 1 || n.Row != 0).Any(n => n.EndTime != 3000))
                throw new Exception("Recorder shortened outside overlap, across rows, or by an extra step");
            history.RestoreState(-1);
            if (editor.HitObjects.Count != 3 || ((ShredHitObject)editor.HitObjects[0]).EndTime != 2000)
                throw new Exception("Recorder tail undo failed");
        }
        Console.WriteLine("PASS Actual Step Recorder: shorten only inside tail, exact new head, other row/fret intact, undo restores length");
    }
    private sealed class Clock : IAdjustableClock
    {
        public double CurrentTime { get; private set; }
        public double Rate { get; set; } = 1;
        public bool IsRunning { get; private set; }
        public bool Seek(double time) { CurrentTime = time; return true; }
        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void Reset() { CurrentTime = 0; IsRunning = false; }
        public void ResetSpeedAdjustments() => Rate = 1;
    }
}
