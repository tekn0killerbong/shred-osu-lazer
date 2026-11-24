using System.IO.Compression;
using System.Text;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Shred;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.GameplayTest;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;

internal partial class Revision54EditorRateChecks : OsuGame
{
    private readonly string folder;
    private readonly DateTime started = DateTime.UtcNow;
    private Exception failure;
    private bool passed;
    private int checks;
    private IEnumerator<Func<bool>> steps;
    private Func<bool> pending;
	private bool creationOnly;
	private bool timingOnly;
	private bool composeOnly;
	private bool revision57Only;
	private bool revision58Only;
    private bool revision59Only;
    private bool revision60Only;
    private bool revision61Only;
    private bool revision62Only;
    private bool revision63Only;
    private bool revision64Only;
    private bool revision65Only;
	private bool revision66Only;
	private bool revision67Only;
	private bool revision68Only;
	private bool revision70Only;
	private bool restart57;
	private osu.Framework.Input.UserInputManager pointerInput;
	protected override osu.Framework.Input.UserInputManager CreateUserInputManager()
	{
		pointerInput = base.CreateUserInputManager();
		return pointerInput;
	}

    private Revision54EditorRateChecks(string folder) => this.folder = folder;
    protected override Storage CreateStorage(GameHost host, Storage defaultStorage) => new NativeStorage(folder);
    protected override void LoadComplete()
    {
        base.LoadComplete();
        steps = runSteps();
		if (creationOnly) steps = runCreationSteps();
		if (timingOnly) steps = runTimingSteps();
		if (composeOnly) steps = runComposerSteps();
		if (revision57Only) steps = runRevision57Steps();
		if (revision58Only) steps = runRevision58Steps();
        if (revision59Only) steps = runRevision59Steps();
        if (revision60Only) steps = runRevision60EditorSteps();
        if (revision61Only) steps = runRevision61EditorSteps();
        if (revision62Only) steps = runRevision62EditorSteps();
        if (revision63Only) steps = runRevision63AudioSteps();
        if (revision64Only) steps = runRevision64EditorSteps();
        if (revision65Only) steps = runRevision65EditorSteps();
		if (revision66Only) steps = runRevision66EditorSteps();
		if (revision67Only) steps = runRevision67EditorSteps();
		if (revision68Only) steps = runRevision68EditorSteps();
		if (revision70Only) steps = runRevision70EditorSteps();
        Scheduler.AddDelayed(tick, 20, true);
    }
    private void tick()
    {
        if (passed || failure != null) return;
        try
        {
            if ((DateTime.UtcNow - started).TotalSeconds > 300)
                throw new Exception("Timed out on " + ScreenStack?.CurrentScreen?.GetType().Name);
            if (pending != null && !pending()) return;
            if (steps.MoveNext()) { pending = steps.Current; return; }
            passed = true;
            if (revision70Only) { Console.WriteLine($"PASS v0.1.1 (v70): {checks} editor zoom and scroll-default checks"); Exit(); return; }
            if (revision68Only) { Console.WriteLine($"PASS v68: {checks} editor checks"); Exit(); return; }
            if (revision67Only) { Console.WriteLine($"PASS v67: {checks} musical group checks"); Exit(); return; }
            if (revision66Only) { Console.WriteLine($"PASS v66.1: {checks} group placement and timing checks"); Exit(); return; }
            if (revision65Only) { Console.WriteLine($"PASS v65: {checks} timing and marquee checks"); Exit(); return; }
            if (revision64Only) { Console.WriteLine($"PASS v64: {checks} selection and playback scope checks"); Exit(); return; }
            if (revision63Only) { Console.WriteLine($"PASS v63: {checks} editor audio checks"); Exit(); return; }
            if (revision62Only) { Console.WriteLine($"PASS v62: {checks} binding and navigation checks"); Exit(); return; }
            if (revision61Only) { Console.WriteLine($"PASS v61: {checks} editor checks"); Exit(); return; }
			if (revision60Only) Console.WriteLine($"PASS v60: {checks} editor Accuracy tooltip checks");
            else if (revision59Only) Console.WriteLine($"PASS v59: {checks} editor and binding checks");
            else if (revision58Only) Console.WriteLine($"PASS v58: {checks} slider, rate, pitch and absolute-scroll checks across five rulesets");
			else if (revision57Only) Console.WriteLine($"PASS v57: {checks} Select, pitch and persistence checks; restart={restart57}");
			else if (composeOnly) Console.WriteLine($"PASS v56: {checks} composer input checks");
			else if (timingOnly) Console.WriteLine($"PASS v56: {checks} timing and composer checks across all five rulesets");
			else if (creationOnly) Console.WriteLine($"PASS v54: {checks} editor difficulty creation and layout checks");
			else Console.WriteLine($"PASS v54: {checks} editor/Test speed, pitch and layout checks across all five rulesets");
            Exit();
        }
        catch (Exception error) { failure = error; Console.WriteLine(error); Exit(); }
    }
    private void check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        checks++;
        Console.WriteLine("PASS " + label);
    }
    private static bool close(double actual, double expected) => Math.Abs(actual - expected) < 0.001;
    private void checkClock(GameplayClockContainer clock, double rate, string label, bool pitched = false)
    {
		double tempo = rate;
		double frequency = 1;
		if (pitched) { tempo = 1; frequency = rate; }
        check(close(clock.AdjustmentsFromMods.AggregateTempo.Value, tempo), label + " tempo " + tempo);
        check(close(clock.AdjustmentsFromMods.AggregateFrequency.Value, frequency), label + " frequency " + frequency);
    }
    private IEnumerator<Func<bool>> runSteps()
    {
        yield return () => ScreenStack?.CurrentScreen is MainMenu;
        string archive = createFixture();
        Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(archive), "rates.osz") });
        yield return () => import.IsCompleted;
        import.GetAwaiter().GetResult();
        foreach (string mode in new[] { "osu", "taiko", "fruits", "mania", "shred" })
        {
            BeatmapInfo info = null;
            yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-" + mode))?.Ruleset.ShortName == mode;
            Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true);
            Ruleset.Value = info.Ruleset;
            ScreenStack.Push(new EditorLoader());
            yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse;
            Editor editor = (Editor)ScreenStack.CurrentScreen;
            EditorClock editorClock = editor.Dependencies.Get<EditorClock>();
            PlaybackRateControl tabs = editor.ChildrenOfType<PlaybackRateControl>().Single();
			yield return () => true;
			PlaybackPitchControl header = editor.ChildrenOfType<PlaybackPitchControl>().Single();
			yield return () => header.IsLoaded;
			PlaybackControl playback = editor.ChildrenOfType<PlaybackControl>().Single();
			SwitchButton toggle = header.ChildrenOfType<SwitchButton>().Single();
			check(toggle.Current.Value, mode + " Pitched defaults on and keeps the previous editor choice");
			check(header.Children.OfType<OsuSpriteText>().Select(t => t.Text.ToString()).SequenceEqual(new[] { "Playback speed", "|", "Pitched" }), mode + " requested header order");
			check(header.ScreenSpaceDrawQuad.TopLeft.X >= playback.ScreenSpaceDrawQuad.TopLeft.X
				&& header.ScreenSpaceDrawQuad.BottomRight.X <= playback.ScreenSpaceDrawQuad.BottomRight.X, mode + " header and toggle fit playback section");
			check(toggle.ScreenSpaceDrawQuad.TopLeft.X >= header.Children.OfType<OsuSpriteText>().Last().ScreenSpaceDrawQuad.BottomRight.X, mode + " toggle follows Pitched text");
			check(header.ScreenSpaceDrawQuad.BottomRight.Y <= tabs.ScreenSpaceDrawQuad.TopLeft.Y + 1, mode + " speed percentages remain below header");
			checkPlaybackAlignment(editor, mode);
			foreach (bool pitched in new[] { false, true })
            foreach (double rate in new[] { 0.25, 0.5, 0.75, 1.0 })
            {
				toggle.Current.Value = pitched;
                editorClock.Stop();
                editorClock.Seek(5000);
                tabs.Current.Value = rate;
				double expectedTempo = rate;
				double expectedFrequency = 1;
				if (pitched) { expectedTempo = 1; expectedFrequency = rate; }
				check(close(editorClock.AudioAdjustments.AggregateTempo.Value, expectedTempo), mode + " editor tempo " + rate + ", pitched=" + pitched);
				check(close(editorClock.AudioAdjustments.AggregateFrequency.Value, expectedFrequency), mode + " editor frequency " + rate + ", pitched=" + pitched);
				editorClock.Start();
				yield return () => close(((osu.Framework.Timing.IClock)editorClock).Rate, rate);
				check(close(Beatmap.Value.Track.AggregateTempo.Value, expectedTempo) && close(Beatmap.Value.Track.AggregateFrequency.Value, expectedFrequency), mode + " editor audio track receives pitch mode");
				editorClock.Stop();
                editor.TestGameplay();
                yield return () => ScreenStack.CurrentScreen is EditorPlayer p && p.IsLoaded;
                EditorPlayer player = (EditorPlayer)ScreenStack.CurrentScreen;
                GameplayClockContainer clock = player.ChildrenOfType<GameplayClockContainer>().Single();
                yield return () => !clock.IsPaused.Value && clock.CurrentTime > 5100;
                checkClock(clock, rate, mode + " Test", pitched);
				check(close(Beatmap.Value.Track.AggregateTempo.Value, expectedTempo) && close(Beatmap.Value.Track.AggregateFrequency.Value, expectedFrequency), mode + " Test audio track receives pitch mode");
                double firstTime = clock.CurrentTime;
                DateTime realStart = DateTime.UtcNow;
                yield return () => (DateTime.UtcNow - realStart).TotalMilliseconds > 500;
                double measured = (clock.CurrentTime - firstTime) / (DateTime.UtcNow - realStart).TotalMilliseconds;
                check(Math.Abs(measured - rate) < 0.09, $"{mode} advancing clock {measured:F3} at {rate}");
                clock.Stop();
                yield return () => clock.IsPaused.Value;
                checkClock(clock, rate, mode + " paused Test", pitched);
                clock.Start();
                yield return () => !clock.IsPaused.Value;
                checkClock(clock, rate, mode + " resumed Test", pitched);
                if (rate == 0.5)
                {
                    check(player.Restart(true), mode + " retry accepted");
                    yield return () => ScreenStack.CurrentScreen is EditorPlayer p && p != player && p.IsLoaded;
                    checkClock(clock, 1, mode + " previous Test released tempo before retry");
                    player = (EditorPlayer)ScreenStack.CurrentScreen;
                    clock = player.ChildrenOfType<GameplayClockContainer>().Single();
                    yield return () => !clock.IsPaused.Value;
                    checkClock(clock, rate, mode + " retried Test", pitched);
                }
                player.Exit();
                yield return () => ScreenStack.CurrentScreen == editor;
                checkClock(clock, 1, mode + " exited Test released tempo");
                check(close(tabs.Current.Value, rate), mode + " editor keeps selected rate");
				check(toggle.Current.Value == pitched, mode + " editor keeps Pitched after Test");
                editorClock.Start();
                yield return () => close(((osu.Framework.Timing.IClock)editorClock).Rate, rate);
                check(close(((osu.Framework.Timing.IClock)editorClock).Rate, rate), mode + " editor resumes without multiplying rates");
                editorClock.Stop();
            }
			// Toggle while the editor is playing: the speed must not be multiplied.
			tabs.Current.Value = 0.5;
			editorClock.Start();
			foreach (bool pitched in new[] { false, true, false, true })
			{
				toggle.Current.Value = pitched;
				yield return () => true;
				check(close(((osu.Framework.Timing.IClock)editorClock).Rate, 0.5), mode + " live pitch toggle keeps half speed");
				double frequency = 1;
				if (pitched) frequency = 0.5;
				check(close(Beatmap.Value.Track.AggregateFrequency.Value, frequency), mode + " live pitch toggle updates the audio track");
			}
			editorClock.Stop();
			editor.Mode.Value = EditorScreenMode.Timing;
			yield return () => tabs.Current.Disabled;
			check(close(tabs.Current.Value, 1) && close(editorClock.AudioAdjustments.AggregateFrequency.Value, 1)
				&& close(editorClock.AudioAdjustments.AggregateTempo.Value, 1), mode + " timing mode still forces normal speed and pitch");
			editor.Mode.Value = EditorScreenMode.Compose;
			yield return () => !tabs.Current.Disabled;
            // Leave at half speed; a normal play must not inherit editor tempo.
            tabs.Current.Value = 0.5;
            editor.Exit();
            yield return () => ScreenStack.CurrentScreen is MainMenu;
            ScreenStack.Push(new PlayerLoader(() => new SoloPlayer(new PlayerConfiguration { ShowResults = false })));
            yield return () => ScreenStack.CurrentScreen is Player p && p.IsLoaded;
            Player normalPlayer = (Player)ScreenStack.CurrentScreen;
            GameplayClockContainer normalClock = normalPlayer.ChildrenOfType<GameplayClockContainer>().Single();
            checkClock(normalClock, 1, mode + " normal gameplay stays at 1x");
			check(close(Beatmap.Value.Track.AggregateTempo.Value, 1) && close(Beatmap.Value.Track.AggregateFrequency.Value, 1), mode + " normal gameplay audio is clean");
            normalPlayer.Exit();
            yield return () => ScreenStack.CurrentScreen is MainMenu;
        }
    }
    private string createFixture()
    {
        string path = Path.Combine(folder, "rates.osz");
        using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (BinaryWriter wav = new BinaryWriter(zip.CreateEntry("tone.wav").Open()))
        {
            const int samples = 8000 * 60;
            wav.Write(Encoding.ASCII.GetBytes("RIFF")); wav.Write(36 + samples * 2);
            wav.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); wav.Write(16);
            wav.Write((short)1); wav.Write((short)1); wav.Write(8000); wav.Write(16000);
            wav.Write((short)2); wav.Write((short)16);
            wav.Write(Encoding.ASCII.GetBytes("data")); wav.Write(samples * 2);
            for (int i = 0; i < samples; i++) wav.Write((short)(400 * Math.Sin(i * 2 * Math.PI * 220 / 8000)));
        }
        string[] modes = { "osu", "taiko", "fruits", "mania", "shred" };
        for (int index = 0; index < modes.Length; index++)
        {
            bool shred = index == 4;
            using StreamWriter writer = new StreamWriter(zip.CreateEntry(modes[index] + ".osu").Open());
            writer.WriteLine(shred ? "shred portable format v1" : "osu file format v14");
            writer.WriteLine($"[General]\nAudioFilename:tone.wav\nMode:{Math.Min(index, 3)}");
            if (shred) writer.WriteLine("CustomRuleset:shred");
            writer.WriteLine($"[Metadata]\nTitle:v54 rate tests\nArtist:shred-tests\nCreator:shred-tests\nVersion:v54-{modes[index]}\nBeatmapID:-1\nBeatmapSetID:-1");
            writer.WriteLine($"[Difficulty]\nCircleSize:{(shred ? 12 : 4)}\nOverallDifficulty:5\nHPDrainRate:2\nSliderMultiplier:1.4\nSliderTickRate:1\n[TimingPoints]\n0,500,4,1,0,50,1,0\n[HitObjects]");
            for (int i = 0; i < 50; i++) writer.WriteLine($"64,192,{2000 + i * 1000},1,0,0:0:0:0:");
            if (shred)
            {
                writer.WriteLine("[RulesetData]\nVersion:2\nAudioFile:dG9uZS53YXY=");
                for (int i = 0; i < 50; i++) writer.WriteLine($"{i},0,0");
            }
        }
        return path;
    }
    public static void Run(bool creationOnly = false, bool timingOnly = false, bool composeOnly = false,
		bool revision57 = false, bool revision58 = false, bool revision59 = false, bool revision60 = false, bool revision61 = false, string persistenceFolder = null, bool restart = false, bool revision62 = false, bool revision63 = false, bool revision64 = false, bool revision65 = false, bool revision66 = false, bool revision67 = false, bool revision68 = false, bool revision70 = false)
    {
        string folder = Path.Combine(Path.GetTempPath(), "shred-v54-rates-" + Guid.NewGuid());
		if (persistenceFolder != null) folder = persistenceFolder;
        Directory.CreateDirectory(Path.Combine(folder, "rulesets"));
        File.Copy(typeof(ShredRuleset).Assembly.Location, Path.Combine(folder, "rulesets", "osu.Game.Rulesets.Shred.dll"), true);
		using (OsuConfigManager config = new OsuConfigManager(new NativeStorage(folder)))
			config.SetValue(OsuSetting.ShowFirstRunSetup, false);
        Console.WriteLine("v54 test data: " + folder);
        using HeadlessGameHost host = new HeadlessGameHost("shred-v54-rates");
        using Revision54EditorRateChecks game = new Revision54EditorRateChecks(folder);
		game.creationOnly = creationOnly;
		game.timingOnly = timingOnly;
		game.composeOnly = composeOnly;
		game.revision57Only = revision57;
		game.revision58Only = revision58;
        game.revision59Only = revision59;
        game.revision60Only = revision60;
        game.revision61Only = revision61;
        game.revision62Only = revision62;
        game.revision63Only = revision63;
        game.revision64Only = revision64;
        game.revision65Only = revision65;
		game.revision66Only = revision66;
		game.revision67Only = revision67;
		game.revision68Only = revision68;
		game.revision70Only = revision70;
		game.restart57 = restart;
        host.Run(game);
        if (!game.passed || game.failure != null) throw new Exception("Editor rate test failed; logs: " + folder, game.failure);
    }
}
