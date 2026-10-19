using osu.Game.Rulesets.Objects;
using osu.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.Shred.Edit;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Shred;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Menu;

internal partial class EditorUiProbe : OsuGame
{
    private readonly string folder;
    private readonly string archive;
    private readonly string difficulty;
    private Task? import;
    private bool opened;
    public static bool TestGameplayMode;
    public static bool Revision45Only;
    public static bool Revision46Only;
    public static bool Revision47Only;
    public static bool Revision48Only;
    public static bool Revision49Only;
    public static bool Revision50Only;
    public static bool Revision51Only;
    public static bool Revision54Only;
    public static bool Revision55Only;
    private IEnumerator<Action> gameplaySkinSteps;
    private bool gameplaySkinReady;
    public static osu.Framework.Input.UserInputManager PointerInput;
    protected override osu.Framework.Input.UserInputManager CreateUserInputManager()
    {
        PointerInput=base.CreateUserInputManager();
        return PointerInput;
    }
    public static string TestSkinArchive;
    public static bool CaptureFrames;
    public static Action FrameAction;
    public static readonly List<double> FrameCosts = new();
    private long frameStarted;
    protected override void Update()
    {
        frameStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        FrameAction?.Invoke();
        base.Update();
    }
    protected override void UpdateAfterChildren()
    {
        base.UpdateAfterChildren();
        if (CaptureFrames) FrameCosts.Add(System.Diagnostics.Stopwatch.GetElapsedTime(frameStarted).TotalMilliseconds);
    }
    public static bool TestAudioMode;
    private List<(osu.Framework.Audio.Sample.ISample sample, double ms)> audioSamples;
    public static string ExportDirectory;
    private Task export;
    private bool testStarted;
    private DateTime testEntered;
    private bool testObserved;
    private double lastTestTime;
    private bool completionObserved;
    public static double TestStartTime = 7000;
    private IEnumerator<Action> revisionSteps;
    private DateTime start = DateTime.UtcNow;
    public static bool Passed;
    public EditorUiProbe(string folder, string archive, string difficulty) { this.folder = folder; this.archive = archive; this.difficulty = difficulty; }
    protected override Storage CreateStorage(GameHost host, Storage defaultStorage) => new NativeStorage(folder);
    protected override void LoadComplete()
    {
        base.LoadComplete();
        Scheduler.AddDelayed(TickProbe, 250, true);
    }
    private void TickProbe()
    {
        if ((DateTime.UtcNow-start).TotalSeconds>70) { Console.WriteLine("UI PROBE timeout: "+ScreenStack?.CurrentScreen?.GetType().Name); Exit();return; }
        if (TestGameplayMode && testStarted)
        {
            try
            {
                if (ScreenStack?.CurrentScreen is osu.Game.Screens.Edit.GameplayTest.EditorPlayer player)
                {
                    if (!testObserved) { testEntered = DateTime.UtcNow; testObserved = true; }
                    var ruleset = player.ChildrenOfType<osu.Game.Rulesets.Shred.UI.DrawableShredRuleset>().Single();
                    if (TestAudioMode)
                    {
                        if (audioSamples == null)
                        {
                            audioSamples = new();
                            var skinSource = ruleset.Dependencies.Get<osu.Game.Skinning.ISkinSource>();
                            foreach (var file in Revision45Checks.Files)
                            {
                                var parts = file.name.Split('-');
                                var sample = skinSource.GetSample(new osu.Game.Audio.HitSampleInfo(parts[1], parts[0]));
                                Revision45Checks.Check(sample != null, "gameplay resolves " + file.name);
                                audioSamples.Add((sample, file.ms));
                                sample.GetChannel().Play();
                            }
                            return;
                        }
                        if (!audioSamples.All(s => s.sample.IsLoaded)) return;
                        foreach (var entry in audioSamples)
                            Revision45Checks.Check(Math.Abs(entry.sample.Length - entry.ms) < 2, "gameplay decoded supplied audio duration: " + entry.sample.Name);
                        Passed = true; Console.WriteLine("PASS Gameplay audio defaults"); Exit(); return;
                    }
                    var score = ruleset.Dependencies.Get<osu.Game.Rulesets.Scoring.ScoreProcessor>();
                    var clock = ruleset.Dependencies.Get<osu.Game.Screens.Play.GameplayClockContainer>();
                    lastTestTime = clock.CurrentTime;
                    completionObserved |= score.HasCompleted.Value;
                    Console.WriteLine($"TEST time={clock.CurrentTime:F0} judged={score.JudgedHits} complete={score.HasCompleted.Value}");
                    Revision39Checks.Check(score.JudgedHits == ruleset.Objects.Sum(n => (n.GetEndTime() < TestStartTime ? 1 : 0) + n.NestedHitObjects.Count(h => h.GetEndTime() < TestStartTime)) + ruleset.Playfield.AllHitObjects.Count(n => n.HitObject.GetEndTime() >= TestStartTime && n.Judged) + ruleset.Playfield.AllHitObjects.SelectMany(n => n.NestedHitObjects).Count(n => n.HitObject.GetEndTime() >= TestStartTime && n.Judged), "v42 Test counts each judgement only once");
                    if (clock.CurrentTime < 21000) Revision39Checks.Check(!score.HasCompleted.Value, "v42 Test remains active before last note");
                    return;
                }
                if (testObserved && ScreenStack?.CurrentScreen is Editor)
                {
                    Revision39Checks.Check(lastTestTime >= 21000 && completionObserved, "v42 Test returns only after final LN tail");
                    Console.WriteLine("PASS UI Test complete without premature exit"); Passed=true; Exit();
                }
            }
            catch (Exception e) { Console.WriteLine("UI CHECK FAILED: " + e); Exit(); }
            return;
        }
        if (ScreenStack?.CurrentScreen is Editor editor)
        {
            if (editor.ReadyForUse && editor.ChildrenOfType<ShredHitObjectComposer>().Any() && editor.ChildrenOfType<ShredStepRecorder>().Any()) {
                if (TestGameplayMode)
                {
                    try
                    {
                        var c = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
                        if (TestSkinArchive != null && !gameplaySkinReady)
                        {
                            gameplaySkinSteps ??= Revision49Checks.ImportSkin(c.Dependencies.Get<osu.Game.Skinning.SkinManager>(), TestSkinArchive).GetEnumerator();
                            if (gameplaySkinSteps.MoveNext()) { gameplaySkinSteps.Current(); return; }
                            gameplaySkinReady = true;
                        }
                        var map = c.Dependencies.Get<EditorBeatmap>();
                        var clock = c.Dependencies.Get<EditorClock>();
                        map.BeginChange();
                        foreach (var n in map.HitObjects.ToArray()) map.Remove(n);
                        for (int i=1;i<=19;i++) map.Add(new osu.Game.Rulesets.Shred.Objects.ShredHitObject { StartTime=i*1000, Row=i%2, Fret=1, Duration=i==6 ? 500 : i==19 ? 2000 : 0 });
                        map.Add(new osu.Game.Rulesets.Shred.Objects.ShredHitObject { StartTime=6500, Duration=2000, Row=0, Fret=2 });
                        map.EndChange(); clock.Stop(); clock.Seek(TestStartTime);
                        var saved = (bool)typeof(Editor).GetMethod("Save", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(editor, null)!;
                        Revision39Checks.Check(saved, "v42 Test fixture saved");
                        testStarted = true;
                        editor.TestGameplay();
                    }
                    catch(Exception e) { Console.WriteLine("UI CHECK FAILED: "+e); Exit(); }
                    return;
                }
                if (Revision55Only) revisionSteps ??= Revision55HistoryChecks.Steps(editor).GetEnumerator();
                if (Revision54Only) revisionSteps ??= Revision54RecorderChecks.Steps(editor).GetEnumerator();
                if (Revision45Only) revisionSteps ??= Revision45UiChecks.Steps(editor).GetEnumerator();
                if (Revision46Only) revisionSteps ??= Revision46UiChecks.Steps(editor).GetEnumerator();
                if (Revision47Only) revisionSteps ??= Revision47UiChecks.Steps(editor).GetEnumerator();
                if (Revision48Only) revisionSteps ??= Revision48UiChecks.Steps(editor).GetEnumerator();
                if (Revision51Only) revisionSteps ??= Revision51UiChecks.Steps(editor).GetEnumerator();
                if (Revision50Only) revisionSteps ??= Revision50UiChecks.Steps(editor).GetEnumerator();
                if (Revision49Only) revisionSteps ??= Revision49Checks.Steps(editor).GetEnumerator();
                revisionSteps ??= Revision39UiChecks.Steps(editor).Concat(Revision41UiChecks.Steps(editor)).Concat(Revision44UiChecks.Steps(editor)).GetEnumerator();
                try
                {
                    if (revisionSteps.MoveNext()) { revisionSteps.Current(); return; }
                    Console.WriteLine("PASS UI editor ready and requested interactions " + editor.Mode.Value);
                    Passed = true; Exit();
                }
                catch (Exception e) { Console.WriteLine("UI CHECK FAILED: " + e); Exit(); }

            }
            return;
        }
        if(export!=null)
        {
            if(!export.IsCompleted) return;
            try { export.GetAwaiter().GetResult(); Passed=true; }
            catch(Exception e) {Console.WriteLine("EXPORT FAILED: "+e);}
            Exit(); return;
        }
        if (ScreenStack?.CurrentScreen is not MainMenu || opened) return;
        if (import==null) { import=BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(archive), Path.GetFileName(archive)) });Console.WriteLine("UI importing");return; }
        if (!import.IsCompleted)return;
        import.GetAwaiter().GetResult();
        var info=BeatmapManager.QueryBeatmap(b=>b.DifficultyName==difficulty)!;
        if (info == null || info.Ruleset.ShortName != "shred") return;
        if(ExportDirectory!=null)
        {
            opened=true;
            export=Revision43ExportChecks.Run(BeatmapManager,info,folder,archive,ExportDirectory);
            return;
        }
        Beatmap.Value=BeatmapManager.GetWorkingBeatmap(info,refetch:true);
        Ruleset.Value=info.Ruleset;
        Console.WriteLine("UI opening "+info.Ruleset.InstantiationInfo);
        opened=true;
        ScreenStack.Push(new EditorLoader());
    }
    public static void Run(string archive, string difficulty)
    {
        if (Revision47Only) TestSkinArchive ??= Revision47Checks.CreateUniversalSkin();
        string folder=Path.Combine(Path.GetTempPath(),"shred-ui-"+Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(folder,"rulesets"));
        File.Copy(typeof(ShredRuleset).Assembly.Location,Path.Combine(folder,"rulesets","osu.Game.Rulesets.Shred.dll"));
        Console.WriteLine("UI data "+folder);
        using var host=new HeadlessGameHost("shred-ui-probe");
        using var game=new EditorUiProbe(folder,Path.GetFullPath(archive), difficulty);
        host.Run(game);
        if(!Passed)throw new Exception("UI probe failed; logs: "+folder);
    }
}
