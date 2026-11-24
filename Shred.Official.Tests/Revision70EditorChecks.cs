using osu.Framework.Allocation;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.UI;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Menu;
using osuTK;

internal partial class Revision54EditorRateChecks
{
	private IEnumerator<Func<bool>> runRevision70EditorSteps()
	{
		yield return () => ScreenStack?.CurrentScreen is MainMenu;
		Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(createFixture()), "v70.osz") });
		yield return () => import.IsCompleted;
		import.GetAwaiter().GetResult();
		var info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred");
		yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred"))?.Ruleset.ShortName == "shred";
		Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true);
		Ruleset.Value = info.Ruleset;
		ScreenStack.Push(new EditorLoader());
		yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse;
		var editor = (Editor)ScreenStack.CurrentScreen;
		var field = editor.ChildrenOfType<ShredEditorPlayfield>().Single();
		var map = editor.Dependencies.Get<EditorBeatmap>();
		var clock = editor.Dependencies.Get<EditorClock>();
		yield return () => field.DrawWidth > 0 && editor.ChildrenOfType<PlaybackRateControl>().Any(c => c.IsLoaded);
		var control = editor.ChildrenOfType<PlaybackRateControl>().Single();
		var pitch = editor.ChildrenOfType<PlaybackPitchControl>().Single().ChildrenOfType<SwitchButton>().Single();
		clock.Stop();
		clock.Seek(5000);
		yield return () => true;

		check(ShredScrollSpeed.Setting.Default == 8, "scroll setting defaults to 8");
		int savedSpeed = ShredScrollSpeed.Speed;
		ShredScrollSpeed.Setting.Value = 28;
		ShredScrollSpeed.Setting.SetDefault();
		check(ShredScrollSpeed.Speed == 8, "reset restores scroll speed 8");
		check(close(ShredScrollSpeed.ApproachTime, 1435.625), "default gameplay travel lasts 1435.625 real milliseconds");
		check(close(ShredEditorOperations.VisibleTime(1), 574.25), "existing editor scale at 100% is preserved");

		foreach (double zoom in new[] { .25, .5, 1 })
		{
			map.TimelineZoom = zoom;
			control.Current.Value = 1;
			yield return () => true;
			double visibleTime = field.VisibleTime;
			Vector2[] heads = Enumerable.Range(0, 13).Select(c => field.PositionAt(c, field.CurrentTime + 150)).ToArray();
			Vector2[] tails = Enumerable.Range(0, 13).Select(c => field.PositionAt(c, field.CurrentTime + 350)).ToArray();
			foreach (bool pitched in new[] { true, false })
			foreach (double rate in new[] { .01, .5, 1, 2 })
			{
				pitch.Current.Value = pitched;
				control.Current.Value = rate;
				yield return () => close(field.PlaybackRate, rate);
				foreach (bool running in new[] { false, true })
				{
					if (running) clock.Start();
					else clock.Stop();
					yield return () => !running || close(((IClock)clock).Rate, rate);
					check(close(field.VisibleTime, visibleTime) && close(map.TimelineZoom, zoom), $"native zoom stays fixed: zoom={zoom}, rate={rate}, pitched={pitched}, playing={running}");
					for (int column = 0; column < 13; column++)
					{
						double time = field.CurrentTime + 150;
						Vector2 head = field.PositionAt(column, time);
						Vector2 tail = field.PositionAt(column, field.CurrentTime + 350);
						check((head - heads[column]).Length < .01 && (tail - tails[column]).Length < .01, "head/LN/wham spacing unchanged in column " + column);
						check(Math.Abs(field.TimeAt(field.ToScreenSpace(head), column) - time) < .05, "mouse placement still targets the same map time in column " + column);
					}
				}
				clock.Stop();
				clock.Seek(5000);
				yield return () => true;
			}
		}

		control.Current.Value = 1;
		map.TimelineZoom = .5;
		yield return () => true;
		Vector2 before = field.PositionAt(1, field.CurrentTime + 150) - field.Receiver(0, 1);
		map.TimelineZoom = 1;
		yield return () => true;
		Vector2 after = field.PositionAt(1, field.CurrentTime + 150) - field.Receiver(0, 1);
		check((after - before * 2).Length < .01, "changing native zoom still changes note spacing");
		foreach (int speed in new[] { 8, 20, 40 })
		{
			ShredScrollSpeed.Setting.Value = speed;
			check(close(field.VisibleTime, 574.25), "gameplay scroll preference does not change editor zoom: " + speed);
		}
		ShredScrollSpeed.Setting.Value = savedSpeed;
	}
}
