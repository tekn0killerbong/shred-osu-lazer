using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Input.StateChanges;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Beatmaps;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Timing;
using osu.Game.Screens.Edit.Timing.Multiselect;
using osu.Game.Screens.Menu;
using osuTK;
using osuTK.Input;

internal partial class Revision54EditorRateChecks
{
	private IEnumerator<Func<bool>> runRevision66EditorSteps()
	{
		yield return () => ScreenStack?.CurrentScreen is MainMenu;
		// Regression coverage for v66.1 millisecond movement with the v67 option off.
		File.WriteAllText(Path.Combine(folder, "shred-editor.ini"), "Enabled = False\nReturnOnPause = False\nSnapMovedObjects = False\n");
		Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(createFixture()), "v66.1.osz") });
		yield return () => import.IsCompleted;
		import.GetAwaiter().GetResult();
		var info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred");
		yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred"))?.Ruleset.ShortName == "shred";
		Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true); Ruleset.Value = info.Ruleset;
		ScreenStack.Push(new EditorLoader());
		yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse;
		var editor = (Editor)ScreenStack.CurrentScreen;
		var composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
		var map = editor.Dependencies.Get<EditorBeatmap>();
		var clock = editor.Dependencies.Get<EditorClock>();
		var divisor = composer.Dependencies.Get<BindableBeatDivisor>();
		var history = (EditorChangeHandler)editor.Dependencies.Get<IEditorChangeHandler>();
		var container = (ShredBlueprintContainer)composer.BlueprintContainer;
		composer.StepRecorder.Enabled.Value = false; clock.Stop();
		yield return () => true;

		ShredHitObject[] fixture()
		{
			map.BeginChange(); map.Clear();
			foreach (var group in map.ControlPointInfo.Groups.ToArray()) map.ControlPointInfo.RemoveGroup(group);
			foreach (var pair in new[] { (0d, 500d), (4000d, 720d), (8000d, 480d) })
				map.ControlPointInfo.GroupAt(pair.Item1, true).Add(new TimingControlPoint { BeatLength = pair.Item2 });
			var notes = new[] {
				new ShredHitObject { StartTime = 1000, Fret = 1, Duration = 250, HeadSnapDivisor = 1, TailSnapDivisor = 2 },
				new ShredHitObject { StartTime = 1125, Fret = 2, HeadSnapDivisor = 4 },
				new ShredHitObject { StartTime = 1083.333333015442, Row = 1, Fret = 1, HeadSnapDivisor = 6, Duration = 83.333333969116, TailSnapDivisor = 6 },
				new ShredHitObject { StartTime = 1187.5, Fret = 3, HeadSnapDivisor = 8 },
				new ShredHitObject { StartTime = 1250, Fret = 4, HeadSnapDivisor = 2 },
				new ShredHitObject { StartTime = 1500, Row = 1, Fret = 2, HeadSnapDivisor = 1 }
			};
			map.AddRange(notes); map.EndChange(); map.SelectedHitObjects.Clear(); map.SelectedHitObjects.AddRange(notes);
			return notes;
		}
		(int column, double start, double end)[] snapshot(IEnumerable<ShredHitObject> notes) => notes.OrderBy(n => n.Column).Select(n => (n.Column, n.StartTime, n.EndTime)).ToArray();
		void assertShift(IEnumerable<ShredHitObject> objects, (int column, double start, double end)[] before, double delta, string label)
		{
			var after = snapshot(objects);
			check(after.Length == before.Length, label + " keeps every note");
			for (int i = 0; i < before.Length; i++)
				check(after[i].column == before[i].column && close(after[i].start, before[i].start + delta) && close(after[i].end, before[i].end + delta),
					label + $" column {before[i].column}: head/tail translate together; expected {before[i].start + delta:R}, got {after[i].start:R}");
		}
		void copy(bool native)
		{
			if (native) typeof(Editor).GetMethod("Copy", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null);
			else container.ClipboardCommand(Key.C);
		}
		void paste(bool native)
		{
			if (native) typeof(Editor).GetMethod("Paste", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null);
			else container.ClipboardCommand(Key.V);
		}
		foreach (bool native in new[] { false, true })
		foreach (int display in new[] { 4, 8, 16, 6, 12, 24 })
		foreach (bool acrossBpm in new[] { false, true })
		{
			var before = snapshot(fixture()); divisor.SetArbitraryDivisor(display); yield return () => true;
			double target = 2000 + 500d / display;
			if (acrossBpm) target = 4000 + 720d / display;
			target = composer.SnapTime(target); clock.Seek(target);
			copy(native); paste(native); yield return () => true;
			assertShift(map.SelectedHitObjects.Cast<ShredHitObject>(), before, target - 1000, $"paste native={native} spacing={display} acrossBPM={acrossBpm}");
			check(map.HitObjects.Count == 12, "pasting a mixed group preserves all originals and copies");
			history.RestoreState(-1); yield return () => true;
			assertShift(map.HitObjects.Cast<ShredHitObject>(), before, 0, "paste undo");
			history.RestoreState(1); yield return () => true;
			assertShift(map.HitObjects.Cast<ShredHitObject>().Where(n => n.StartTime >= target - .001), before, target - 1000, "paste redo");
			paste(native); yield return () => true;
			check(map.HitObjects.Count == 12, "repaste does not leave duplicate heads");
			assertShift(map.HitObjects.Cast<ShredHitObject>().Where(n => n.StartTime >= target - .001), before, target - 1000, "repaste preserves LN lengths");
		}

		var recorderUi = editor.ChildrenOfType<ShredStepRecorderToggle>().Single();
		check(recorderUi.ChildrenOfType<OsuCheckbox>().Count() == 3, "v67 restores the optional group snapping toggle");
		foreach (int display in new[] { 8, 16, 12 })
		{
			var before = snapshot(fixture()); divisor.SetArbitraryDivisor(display); yield return () => true;
			double expected = composer.Grid.NextStep(1500) - 1000;
			container.ClipboardCommand(Key.D); yield return () => true;
			assertShift(map.SelectedHitObjects.Cast<ShredHitObject>(), before, expected, "Ctrl+D follows last note using current spacing=" + display);
			history.RestoreState(-1); yield return () => true;
			assertShift(map.HitObjects.Cast<ShredHitObject>(), before, 0, "duplicate undo");
		}

		// Real pointer movement exercises the grid path, including the stored 1/1 metadata.
		foreach (int display in new[] { 8, 16, 12 })
		{
			var moving = fixture(); var before = snapshot(moving);
			divisor.SetArbitraryDivisor(display); clock.Seek(500); map.TimelineZoom = .3; composer.SetSelectTool();
			yield return () => true;
			DateTime layout = DateTime.UtcNow.AddMilliseconds(200); yield return () => DateTime.UtcNow >= layout;
			double target = composer.SnapTime(1000 + 500d / display);
			Vector2 from = composer.Grid.ToScreenSpace(composer.Grid.PositionAt(1, 1000));
			Vector2 to = composer.Grid.ToScreenSpace(composer.Grid.PositionAt(1, target));
			// At this zoom a fine step is smaller than the native drag threshold.
			// Start the gesture with a larger movement, then return to that step.
			new MousePositionAbsoluteInput { Position = from }.Apply(pointerInput.CurrentState, pointerInput); yield return () => true;
			new MouseButtonInput(MouseButton.Left, true).Apply(pointerInput.CurrentState, pointerInput); yield return () => true;
			new MousePositionAbsoluteInput { Position = from + (to - from).Normalized() * 30 }.Apply(pointerInput.CurrentState, pointerInput); yield return () => true;
			new MousePositionAbsoluteInput { Position = to }.Apply(pointerInput.CurrentState, pointerInput); yield return () => true;
			new MouseButtonInput(MouseButton.Left, false).Apply(pointerInput.CurrentState, pointerInput); yield return () => true;
			assertShift(map.HitObjects.Cast<ShredHitObject>(), before, target - 1000, "physical fine-grid drag=" + display);
			history.RestoreState(-1); yield return () => true;
			assertShift(map.HitObjects.Cast<ShredHitObject>(), before, 0, "drag undo");
			history.RestoreState(1); yield return () => true;
			assertShift(map.HitObjects.Cast<ShredHitObject>(), before, target - 1000, "drag redo");
		}

		// Native timeline mutations bypass the Shred drag implementation.
		var nativeBefore = snapshot(fixture()); divisor.SetArbitraryDivisor(16); yield return () => true;
		map.BeginChange();
		foreach (ShredHitObject note in map.HitObjects.ToArray()) { note.StartTime += 31.25; map.Update(note); }
		map.EndChange(); yield return () => true;
		assertShift(map.HitObjects.Cast<ShredHitObject>(), nativeBefore, 31.25, "native movement survives processing without per-note snapping");

		// Keep the independent v66 timing correction, for one or many timing points.
		foreach (bool multiple in new[] { false, true })
		{
			fixture();
			map.BeginChange(); map.Clear();
			var original = new[] {
				new ShredHitObject { StartTime = 1000, Fret = 1, HeadSnapDivisor = 4 },
				new ShredHitObject { StartTime = 1125, Fret = 2, HeadSnapDivisor = 4, Duration = 208.333333015442, TailSnapDivisor = 6 },
				new ShredHitObject { StartTime = 1083.333333015442, Row = 1, Fret = 3, HeadSnapDivisor = 6 },
				new ShredHitObject { StartTime = 1187.5, Fret = 4, HeadSnapDivisor = 8 }
			};
			map.AddRange(original); map.EndChange(); map.SelectedHitObjects.Clear(); map.SelectedHitObjects.AddRange(original);
			divisor.SetArbitraryDivisor(4); clock.Seek(4000); yield return () => true;
			copy(false); paste(false); yield return () => true;
			ShredHitObject[] destination() => map.HitObjects.OfType<ShredHitObject>().Where(n => n.StartTime >= 3900).OrderBy(n => n.Column).ToArray();
			var raw = snapshot(destination());
			check(close(destination()[1].StartTime, 4125), "cross-BPM paste keeps original millisecond intervals");
			editor.Mode.Value = EditorScreenMode.Timing;
			MultiTimingContent content = null;
			yield return () => (content = editor.ChildrenOfType<MultiTimingContent>().SingleOrDefault())?.IsLoaded == true;
			content.Selection.Click(map.ControlPointInfo.GroupAt(4000), false, false);
			if (multiple) content.Selection.Click(map.ControlPointInfo.GroupAt(8000), true, false);
			content.MultipleSettings.MoveObjects.Current.Value = true; yield return () => true;
			if (multiple) content.MultipleSettings.Offset.Current.Value = 4017;
			else content.SingleSettings.ChildrenOfType<FormDiscreteAdjustmentControl<double>>().Single(c => c.Caption.ToString() == "Offset").Current.Value = 4017;
			yield return () => true;
			var timed = destination();
			check(timed.Length == 4 && close(timed[0].StartTime, 4017) && close(timed[1].StartTime, 4197) && close(timed[1].EndTime, 4377)
				&& close(timed[2].StartTime, 4197) && close(timed[3].StartTime, 4137), "Move already placed objects retains mixed-grid resnapping; multiple=" + multiple);
			var timedSnapshot = snapshot(timed);
			history.RestoreState(-1); yield return () => true;
			assertShift(destination(), raw, 0, "timing undo restores raw pasted positions");
			history.RestoreState(1); yield return () => true;
			assertShift(destination(), timedSnapshot, 0, "timing redo restores corrected positions");
			history.RestoreState(-1); yield return () => true;
			content.MultipleSettings.MoveObjects.Current.Value = false;
			content.Selection.MoveBy(17, false); yield return () => true;
			assertShift(destination(), raw, 0, "Move objects unchecked leaves notes in place");
			editor.Mode.Value = EditorScreenMode.Compose; yield return () => true;
		}

		// Retain the historical repair of genuinely old fractional duplicates,
		// without reviving the Ctrl-drag gesture superseded by selection in v64.
		map.BeginChange(); map.Clear();
		foreach (var group in map.ControlPointInfo.Groups.ToArray()) map.ControlPointInfo.RemoveGroup(group);
		map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 }); map.EndChange();
		foreach (var step in fractionalPaste61(editor, composer, map, clock)) yield return step;
		fixture(); divisor.SetArbitraryDivisor(12); clock.Seek(4060); yield return () => true;
		copy(true); paste(true); yield return () => true;
		check(map.HitObjects.Cast<ShredHitObject>().Any(n => close(n.StartTime, 4143.333333015442)), "a valid paste near another grid line keeps its intended fractional timestamp");
		check((bool)typeof(Editor).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null)!, "corrected v66 fixture saves");
		var expectedSaved = snapshot(map.HitObjects.Cast<ShredHitObject>());
		editor.Exit(); yield return () => ScreenStack.CurrentScreen is MainMenu;
		var saved = BeatmapManager.GetWorkingBeatmap(BeatmapManager.QueryBeatmap(b => b.ID == info.ID), refetch: true).GetPlayableBeatmap(new ShredRuleset().RulesetInfo);
		assertShift(saved.HitObjects.Cast<ShredHitObject>(), expectedSaved, 0, "save/reload keeps pasted positions");
	}
}
