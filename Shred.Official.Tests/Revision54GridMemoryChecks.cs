using System.IO.Compression;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Screens.Menu;

internal partial class Revision54GridMemoryChecks : OsuGame
{
	private readonly string folder;
	private readonly string fixture;
	private readonly bool restart;
	private readonly DateTime started = DateTime.UtcNow;
	private IEnumerator<Func<bool>> steps;
	private Func<bool> pending;
	private Exception failure;
	private bool passed;
	private int checks;
	private Editor editor;
	private BindableBeatDivisor divisor;
	private ShredStepRecorder recorder;
	private string mapHash;

	private Revision54GridMemoryChecks(string folder, string fixture, bool restart)
	{
		this.folder = folder;
		this.fixture = fixture;
		this.restart = restart;
	}
	protected override Storage CreateStorage(GameHost host, Storage defaultStorage) => new NativeStorage(folder);
	protected override void LoadComplete()
	{
		base.LoadComplete();
		steps = runSteps();
		Scheduler.AddDelayed(tick, 20, true);
	}
	private void tick()
	{
		try
		{
			if ((DateTime.UtcNow - started).TotalSeconds > 150)
				throw new Exception("Timed out on " + ScreenStack?.CurrentScreen?.GetType().Name);
			if (pending != null && !pending()) return;
			if (steps.MoveNext()) { pending = steps.Current; return; }
			passed = true;
			Console.WriteLine($"PASS v54 grid: {checks} checks, restart={restart}");
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
	private IEnumerable<Func<bool>> open(string name, int expected, BeatDivisorType type, int maximum)
	{
		BeatmapInfo info = null;
		yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == name))?.Ruleset.ShortName == "shred";
		check(info.BeatDivisor == 6, name + " saved map grid remains 1/6");
		mapHash = info.Hash;
		Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true);
		Ruleset.Value = info.Ruleset;
		ScreenStack.Push(new EditorLoader());
		yield return () => ScreenStack.CurrentScreen is Editor ready && ready.ReadyForUse;
		editor = (Editor)ScreenStack.CurrentScreen;
		divisor = editor.Dependencies.Get<BindableBeatDivisor>();
		recorder = editor.ChildrenOfType<ShredStepRecorder>().Single();
		// Let bound sidebar controls and delayed default-preset callbacks settle.
		yield return () => true;
		yield return () => true;
		check(divisor.Value == expected, $"{name} reopened at 1/{expected}, got 1/{divisor.Value}");
		check(divisor.ValidDivisors.Value.Type == type, name + " restored type " + type);
		check(divisor.ValidDivisors.Value.Presets.Max() == maximum, name + " restored preset range");
	}
	private IEnumerable<Func<bool>> close(string name)
	{
		editor.Exit();
		yield return () =>
		{
			PromptForSaveDialog dialog = this.ChildrenOfType<PromptForSaveDialog>().FirstOrDefault(d => d.IsPresent);
			if (dialog != null)
				dialog.ChildrenOfType<PopupDialogDangerousButton>().Single().Action();
			return ScreenStack.CurrentScreen is MainMenu;
		};
		BeatmapInfo saved = BeatmapManager.QueryBeatmap(b => b.DifficultyName == name);
		check(saved.Hash == mapHash && saved.BeatDivisor == 6, name + " exit did not rewrite the beatmap");
	}
	private IEnumerator<Func<bool>> runSteps()
	{
		yield return () => ScreenStack?.CurrentScreen is MainMenu;
		if (restart)
		{
			foreach (Func<bool> step in open("grid-A", 12, BeatDivisorType.Triplets, 12)) yield return step;
			foreach (Func<bool> step in close("grid-A")) yield return step;
			foreach (Func<bool> step in open("grid-B", 4, BeatDivisorType.Common, 16)) yield return step;
			foreach (Func<bool> step in close("grid-B")) yield return step;
			yield break;
		}
		Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(createFixture()), "grids.osz") });
		yield return () => import.IsCompleted;
		import.GetAwaiter().GetResult();
		foreach (Func<bool> step in open("grid-A", 6, BeatDivisorType.Triplets, 12)) yield return step;
		recorder.Enabled.Value = false;
		recorder.ApplyGridCommand(ShredEditorAction.DecreaseGridType);
		yield return () => true;
		check(divisor.Value == 4, "explicit Common change still defaults to 1/4");
		recorder.ApplyGridCommand(ShredEditorAction.IncreaseGridSpacing);
		check(divisor.Value == 8, "spacing command applies with recorder off");
		foreach (Func<bool> step in close("grid-A")) yield return step;
		foreach (Func<bool> step in open("grid-A", 8, BeatDivisorType.Common, 16)) yield return step;
		recorder.Enabled.Value = true;
		recorder.ApplyGridCommand(ShredEditorAction.IncreaseGridType);
		yield return () => true;
		check(divisor.Value == 6, "explicit Triplets change still defaults to 1/6");
		recorder.ApplyGridCommand(ShredEditorAction.IncreaseGridSpacing);
		check(divisor.Value == 12, "spacing command applies with recorder on");
		foreach (Func<bool> step in close("grid-A")) yield return step;
		foreach (Func<bool> step in open("grid-B", 6, BeatDivisorType.Triplets, 12)) yield return step;
		recorder.ApplyGridCommand(ShredEditorAction.DecreaseGridType);
		yield return () => true;
		foreach (Func<bool> step in close("grid-B")) yield return step;
		foreach (Func<bool> step in open("grid-A", 12, BeatDivisorType.Triplets, 12)) yield return step;
		divisor.Value = 1;
		foreach (Func<bool> step in close("grid-A")) yield return step;
		foreach (Func<bool> step in open("grid-A", 1, BeatDivisorType.Triplets, 12)) yield return step;
		divisor.ValidDivisors.Value = BeatDivisorPresetCollection.Custom(24);
		divisor.Value = 8;
		foreach (Func<bool> step in close("grid-A")) yield return step;
		foreach (Func<bool> step in open("grid-A", 8, BeatDivisorType.Custom, 24)) yield return step;
		divisor.ValidDivisors.Value = BeatDivisorPresetCollection.TRIPLETS;
		yield return () => true;
		divisor.Value = 12;
		foreach (Func<bool> step in close("grid-A")) yield return step;
	}
	private string createFixture()
	{
		string path = Path.Combine(folder, "grids.osz");
		using ZipArchive source = ZipFile.OpenRead(fixture);
		using ZipArchive target = ZipFile.Open(path, ZipArchiveMode.Create);
		foreach (ZipArchiveEntry entry in source.Entries)
		{
			if (!entry.FullName.EndsWith(".osu"))
			{
				using Stream input = entry.Open();
				using Stream output = target.CreateEntry(entry.FullName).Open();
				input.CopyTo(output);
				continue;
			}
			using StreamReader reader = new StreamReader(entry.Open());
			string text = reader.ReadToEnd();
			foreach (string name in new[] { "grid-A", "grid-B" })
			{
				using StreamWriter writer = new StreamWriter(target.CreateEntry(name + ".osu").Open());
				writer.Write(text.Replace("Version:shred v29 - activer cartes converties", "Version:" + name)
					.Replace("[Metadata]", "[Editor]\nBeatDivisor:6\n[Metadata]"));
			}
		}
		return path;
	}
	public static void Run(string folder, string fixture, bool restart)
	{
		Directory.CreateDirectory(Path.Combine(folder, "rulesets"));
		File.Copy(typeof(ShredRuleset).Assembly.Location, Path.Combine(folder, "rulesets", "osu.Game.Rulesets.Shred.dll"), true);
		// First-run onboarding can force a return to the menu while the test opens its editor.
		using (OsuConfigManager config = new OsuConfigManager(new NativeStorage(folder)))
			config.SetValue(OsuSetting.ShowFirstRunSetup, false);
		Console.WriteLine("v54 grid test data: " + folder);
		using HeadlessGameHost host = new HeadlessGameHost("shred-v54-grids");
		using Revision54GridMemoryChecks game = new Revision54GridMemoryChecks(folder, fixture, restart);
		host.Run(game);
		if (!game.passed) throw new Exception("Grid memory test failed; logs: " + folder, game.failure);
	}
}
