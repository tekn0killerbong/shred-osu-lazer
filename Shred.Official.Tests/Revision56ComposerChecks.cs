using osu.Framework.Allocation;
using osu.Framework.Input.StateChanges;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Menu;
using osuTK.Input;

internal partial class Revision54EditorRateChecks
{
	private IEnumerator<Func<bool>> runComposerSteps()
	{
		yield return () => ScreenStack?.CurrentScreen is MainMenu;
		Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(createFixture()), "composer.osz") });
		yield return () => import.IsCompleted;
		import.GetAwaiter().GetResult();
		var info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred");
		yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred"))?.Ruleset.ShortName == "shred";
		Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true);
		Ruleset.Value = info.Ruleset;
		ScreenStack.Push(new EditorLoader());
		yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse;
		Editor editor = (Editor)ScreenStack.CurrentScreen;
		foreach (Func<bool> step in tailDragSteps(editor)) yield return step;
		foreach (Func<bool> step in tailHandleSteps(editor)) yield return step;
		foreach (Func<bool> step in timelineSelectionSteps(editor)) yield return step;
		foreach (Func<bool> step in duplicateSteps(editor)) yield return step;
		foreach (Func<bool> step in tailInsertionSteps(editor)) yield return step;
		foreach (Func<bool> step in emptyNavigationSteps(editor)) yield return step;
		foreach (Func<bool> step in redoShortcutSteps(editor)) yield return step;
		foreach (Func<bool> step in rangeSelectionSteps(editor)) yield return step;
	}

	private IEnumerable<Func<bool>> rangeSelectionSteps(Editor editor)
	{
		ShredHitObjectComposer composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
		EditorBeatmap map = editor.Dependencies.Get<EditorBeatmap>();
		EditorClock clock = editor.Dependencies.Get<EditorClock>();
		composer.StepRecorder.Enabled.Value = false;
		composer.SetSelectTool();
		map.TimelineZoom = 0.5;
		clock.Stop(); clock.Seek(750);
		map.BeginChange();
		foreach (var note in map.HitObjects.ToArray()) map.Remove(note);
		ShredHitObject[] notes = Enumerable.Range(0, 9).Select(i => new ShredHitObject { StartTime = 1000 + i * 125, Row = 0, Fret = i % 5 + 1 }).ToArray();
		ShredHitObject[] chordPartners = notes.Select(n => new ShredHitObject { StartTime = n.StartTime, Row = 1, Fret = n.Fret }).ToArray();
		foreach (ShredHitObject note in notes) map.Add(note);
		foreach (ShredHitObject note in chordPartners) map.Add(note);
		map.EndChange();
		yield return () => true;
		// A range made by a rectangle/selection restore has no new click anchor.
		map.SelectedHitObjects.Clear(); map.SelectedHitObjects.AddRange(notes.Skip(3).Take(3));
		map.SelectedHitObjects.AddRange(chordPartners.Skip(3).Take(3));
		yield return () => true;
		foreach (int index in new[] { 1, 7, 3, 0, 8 })
		{
			new MousePositionAbsoluteInput { Position = composer.Grid.ToScreenSpace(composer.Grid.PositionAt(notes[index].Column, notes[index].StartTime)) }
				.Apply(pointerInput.CurrentState, pointerInput);
			yield return () => true;
			new KeyboardKeyInput(Key.ShiftLeft, true).Apply(pointerInput.CurrentState, pointerInput);
			new MouseButtonInput(MouseButton.Left, true).Apply(pointerInput.CurrentState, pointerInput);
			new MouseButtonInput(MouseButton.Left, false).Apply(pointerInput.CurrentState, pointerInput);
			new KeyboardKeyInput(Key.ShiftLeft, false).Apply(pointerInput.CurrentState, pointerInput);
			yield return () => true;
			int from = 1;
			int to = 7;
			if (index == 1) to = 5;
			if (index == 0 || index == 8) from = 0;
			if (index == 8) to = 8;
			check(map.SelectedHitObjects.OrderBy(n => n.StartTime).ThenBy(n => ((ShredHitObject)n).Row).SequenceEqual(
				notes.Skip(from).Take(to - from + 1).Concat(chordPartners.Skip(from).Take(to - from + 1)).OrderBy(n => n.StartTime).ThenBy(n => n.Row)),
				"successive real Shift+click " + index + " extends from opposite selection boundary; selected=" + string.Join(",", map.SelectedHitObjects.Select(n => n.StartTime)));
			check(map.SelectedHitObjects.Cast<ShredHitObject>().GroupBy(n => n.StartTime).All(g => g.Count() == 2 && g.Select(n => n.Row).Distinct().Count() == 2),
				"Shift+click includes complete chords at both boundaries and throughout the range");
			check(close(clock.CurrentTimeAccurate, 750), "Shift+click leaves the grid stationary");
		}
	}
}
