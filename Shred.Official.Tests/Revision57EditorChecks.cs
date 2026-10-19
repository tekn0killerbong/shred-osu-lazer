using osu.Framework.Allocation;
using osu.Framework.Input.StateChanges;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Rulesets.Edit.Tools;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.GameplayTest;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osuTK.Input;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

internal partial class Revision54EditorRateChecks
{
	private void revision57Key(Key key, bool down) => new KeyboardKeyInput(key, down).Apply(pointerInput.CurrentState, pointerInput);
	private void revision57Tap(Key key) { revision57Key(key, true); revision57Key(key, false); }
	private void revision57Click(osuTK.Vector2 position)
	{
		new MousePositionAbsoluteInput { Position = position }.Apply(pointerInput.CurrentState, pointerInput);
		new MouseButtonInput(MouseButton.Left, true).Apply(pointerInput.CurrentState, pointerInput);
		new MouseButtonInput(MouseButton.Left, false).Apply(pointerInput.CurrentState, pointerInput);
	}

	private IEnumerator<Func<bool>> runRevision57Steps()
	{
		yield return () => ScreenStack?.CurrentScreen is MainMenu;
		if (!restart57)
		{
			Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(createFixture()), "v57.osz") });
			yield return () => import.IsCompleted;
			import.GetAwaiter().GetResult();
		}
		bool remembered = !restart57;
		string[] modes = { "osu", "taiko", "fruits", "mania", "shred" };
		if (restart57) modes = new[] { "taiko", "osu" };
		int index = 0;
		foreach (string mode in modes)
		{
			BeatmapInfo info = null;
			yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-" + mode))?.Ruleset.ShortName == mode;
			Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true);
			Ruleset.Value = info.Ruleset;
			ScreenStack.Push(new EditorLoader());
			yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse;
			Editor editor = (Editor)ScreenStack.CurrentScreen;
			DateTime entered = DateTime.UtcNow;
			yield return () => (DateTime.UtcNow - entered).TotalSeconds > 1;
			PlaybackPitchControl header = editor.ChildrenOfType<PlaybackPitchControl>().Single();
			SwitchButton toggle = header.ChildrenOfType<SwitchButton>().Single();
			EditorClock clock = editor.Dependencies.Get<EditorClock>();
			PlaybackRateControl tabs = editor.ChildrenOfType<PlaybackRateControl>().Single();
			check(toggle.Current.Value == remembered, mode + " inherits Pitched=" + remembered + "; restart=" + restart57);
			checkPlaybackAlignment(editor, mode);
			foreach (bool pitched in new[] { false, true })
			{
				if (toggle.Current.Value != pitched) revision57Click(toggle.ScreenSpaceDrawQuad.Centre);
				yield return () => toggle.Current.Value == pitched;
				clock.Stop(); clock.Seek(5000); tabs.Current.Value = 0.5;
				double tempo = 0.5;
				double frequency = 1;
				if (pitched) { tempo = 1; frequency = 0.5; }
				check(close(clock.AudioAdjustments.AggregateTempo.Value, tempo)
					&& close(clock.AudioAdjustments.AggregateFrequency.Value, frequency), mode + " editor half-speed audio follows Pitched=" + pitched);
				check(File.ReadAllText(Path.Combine(folder, "shred-editor-playback.ini")).Contains("Pitched = " + pitched),
					mode + " toggle is immediately persisted outside beatmap/ruleset data");
				editor.TestGameplay();
				yield return () => ScreenStack.CurrentScreen is EditorPlayer player && player.IsLoaded;
				EditorPlayer player = (EditorPlayer)ScreenStack.CurrentScreen;
				GameplayClockContainer gameplay = player.ChildrenOfType<GameplayClockContainer>().Single();
				yield return () => !gameplay.IsPaused.Value && gameplay.CurrentTime > 5100;
				checkClock(gameplay, 0.5, mode + " Test Pitched=" + pitched, pitched);
				check(close(Beatmap.Value.Track.AggregateTempo.Value, tempo) && close(Beatmap.Value.Track.AggregateFrequency.Value, frequency),
					mode + " Test track receives the chosen pitch mode");
				player.Exit();
				yield return () => ScreenStack.CurrentScreen == editor;
				check(toggle.Current.Value == pitched, mode + " retains Pitched after Test");
			}
			remembered = restart57 || index % 2 != 0;
			if (toggle.Current.Value != remembered) revision57Click(toggle.ScreenSpaceDrawQuad.Centre);
			yield return () => toggle.Current.Value == remembered;
			if (mode == "shred")
			{
				foreach (Func<bool> step in selectRecorder57Steps(editor)) yield return step;
				menuItems(editor).Single(i => i.Text.Value.Equals(WebCommonStrings.ButtonsSave)).Action.Value.Invoke();
				yield return () => !hasUnsavedChanges(editor);
			}
			editor.Exit();
			yield return () => ScreenStack.CurrentScreen is MainMenu;
			index++;
		}
		check(File.ReadAllText(Path.Combine(folder, "shred-editor-playback.ini")).Contains("Pitched = " + remembered),
			"last pitch choice remains saved after closing every editor");
	}

	private IEnumerable<Func<bool>> selectRecorder57Steps(Editor editor)
	{
		ShredHitObjectComposer composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
		EditorBeatmap map = editor.Dependencies.Get<EditorBeatmap>();
		EditorClock clock = editor.Dependencies.Get<EditorClock>();
		BindableBeatDivisor divisor = editor.Dependencies.Get<BindableBeatDivisor>();
		IEditorChangeHandler history = editor.Dependencies.Get<IEditorChangeHandler>();
		composer.StepRecorder.Enabled.Value = false;
		clock.Stop(); clock.Seek(1000); divisor.SetArbitraryDivisor(4);
		map.BeginChange();
		foreach (var note in map.HitObjects.ToArray()) map.Remove(note);
		foreach (ControlPointGroup group in map.ControlPointInfo.Groups.ToArray()) map.ControlPointInfo.RemoveGroup(group);
		map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
		map.EndChange();
		composer.StepRecorder.Enabled.Value = true;
		yield return () => composer.StepRecorder.HasFocus;
		revision57Tap(Key.Number1);
		yield return () => true;
		check(composer.BlueprintContainer.CurrentTool is SelectTool && composer.BlueprintContainer.CurrentPlacement == null,
			"1 keeps the actual Select tool with no mouse placement blueprint");
		revision57Key(Key.A, true); revision57Key(Key.Z, true);
		check(map.HitObjects.Count == 2 && map.HitObjects.Cast<ShredHitObject>().All(n => n.Articulation == ShredPracticeArticulation.Pick),
			"Select records Pick frets on both rows");
		revision57Tap(Key.Right);
		revision57Key(Key.A, false); revision57Key(Key.Z, false);
		check(map.HitObjects.Cast<ShredHitObject>().All(n => n.Duration == 125) && close(clock.CurrentTimeAccurate, 1125),
			"Select supports Empty Step LNs and final release stays at their ends");
		history.RestoreState(-1);
		yield return () => true;
		check(map.HitObjects.Count == 0, "Select-recorded chord is one undo");
		history.RestoreState(1);
		yield return () => true;
		check(map.HitObjects.Count == 2 && close(clock.CurrentTimeAccurate, 1125), "redo restores Select-recorded LNs without seeking");
		clock.Seek(750);
		yield return () => true;
		ShredHitObject first = map.HitObjects.Cast<ShredHitObject>().Single(n => n.Row == 0);
		revision57Click(composer.Grid.ToScreenSpace(composer.Grid.PositionAt(first.Column, first.StartTime)));
		yield return () => true;
		check(map.HitObjects.Count == 2 && map.SelectedHitObjects.Contains(first), "Select mouse click selects an existing note without creating one");
		revision57Tap(Key.Number2);
		yield return () => true;
		revision57Key(Key.S, true); revision57Key(Key.S, false);
		check(map.HitObjects.Cast<ShredHitObject>().Single(n => n.Fret == 2).Articulation == ShredPracticeArticulation.Pick,
			"2 still records Pick notes");
		revision57Tap(Key.Number3);
		yield return () => true;
		revision57Key(Key.X, true); revision57Key(Key.X, false);
		check(map.HitObjects.Cast<ShredHitObject>().Single(n => n.Fret == 2 && n.Row == 1).Articulation == ShredPracticeArticulation.HammerPull,
			"3 still records Hammer-pull notes");
		revision57Tap(Key.Number1);
		yield return () => true;
		revision57Key(Key.D, true); revision57Key(Key.D, false);
		check(map.HitObjects.Cast<ShredHitObject>().Single(n => n.Fret == 3).Articulation == ShredPracticeArticulation.Pick
			&& composer.BlueprintContainer.CurrentTool is SelectTool, "returning from Hammer-pull to Select records Pick again");
		composer.StepRecorder.Enabled.Value = false;
		int count = map.HitObjects.Count;
		revision57Key(Key.A, true); revision57Key(Key.A, false);
		check(map.HitObjects.Count == count && composer.BlueprintContainer.CurrentTool is SelectTool,
			"Select without Step Recorder does not create keyboard notes");
	}
}
