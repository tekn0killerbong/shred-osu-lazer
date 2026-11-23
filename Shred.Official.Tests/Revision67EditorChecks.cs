using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Input.StateChanges;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Beatmaps;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Screens.Menu;
using osuTK;
using osuTK.Input;

internal partial class Revision54EditorRateChecks
{
	private IEnumerator<Func<bool>> runRevision67EditorSteps()
	{
		yield return () => ScreenStack?.CurrentScreen is MainMenu;
		Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(createFixture()), "v67.osz") });
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
		composer.StepRecorder.Enabled.Value = false; clock.Stop(); yield return () => true;
		var ui = editor.ChildrenOfType<ShredStepRecorderToggle>().Single();
		check(!ui.SnapMovedToggle.Current.Value, "group snap defaults off");
		check(ui.SnapMovedToggle.ScreenSpaceDrawQuad.TopLeft.Y >= ui.ReturnOnPauseToggle.ScreenSpaceDrawQuad.BottomRight.Y, "group snap toggle appears below Return on pause");
		ui.SnapMovedToggle.Current.Value = true;
		GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();

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
		void expect(IEnumerable<ShredHitObject> objects, (int column, double start, double end)[] before, double target, double ratio, string label)
		{
			var after = snapshot(objects);
			check(after.Length == before.Length, label + " keeps every note");
			for (int i = 0; i < before.Length; i++)
			{
				double head = target + (before[i].start - 1000) * ratio;
				double tail = target + (before[i].end - 1000) * ratio;
				check(after[i].column == before[i].column && close(after[i].start, head) && close(after[i].end, tail),
					label + $" column {before[i].column}: expected {head:R}/{tail:R}, got {after[i].start:R}/{after[i].end:R}");
			}
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
		foreach (int spacing in new[] { 4, 8, 16, 6, 12, 24 })
		foreach (bool acrossBpm in new[] { false, true })
		{
			var before = snapshot(fixture()); divisor.SetArbitraryDivisor(spacing); yield return () => true;
			double target = 2000 + 500d / spacing, ratio = 1;
			if (acrossBpm) { target = 4000 + 720d / spacing; ratio = 720d / 500; }
			target = composer.SnapTime(target); clock.Seek(target); copy(native); paste(native); yield return () => true;
			expect(map.SelectedHitObjects.Cast<ShredHitObject>(), before, target, ratio, $"group paste native={native} spacing={spacing} acrossBPM={acrossBpm}");
			check(map.HitObjects.Count == 12, "all originals remain after paste");
			expect(map.HitObjects.Cast<ShredHitObject>().Where(n => n.StartTime < 2000), before, 1000, 1, "original pattern intact");
			history.RestoreState(-1); yield return () => true;
			expect(map.HitObjects.Cast<ShredHitObject>(), before, 1000, 1, "undo group paste");
			history.RestoreState(1); yield return () => true;
			expect(map.HitObjects.Cast<ShredHitObject>().Where(n => n.StartTime >= target - .001), before, target, ratio, "redo group paste");
			paste(native); yield return () => true;
			check(map.HitObjects.Count == 12, "repeat paste has no duplicate heads");
			expect(map.HitObjects.Cast<ShredHitObject>().Where(n => n.StartTime >= target - .001), before, target, ratio, "repeat paste keeps full LNs");
		}

		foreach (bool native in new[] { false, true })
		{
			var before = snapshot(fixture()); divisor.SetArbitraryDivisor(16); yield return () => true;
			copy(native);
			// Simulate pasting into a map whose source-side BPM is different:
			// clipboard musical coordinates must remain those captured by Copy.
			map.BeginChange(); map.ControlPointInfo.TimingPoints[0].BeatLength = 600; map.UpdateAllHitObjects(); map.EndChange();
			yield return () => true;
			clock.Seek(4045); paste(native); yield return () => true;
			expect(map.SelectedHitObjects.Cast<ShredHitObject>(), before, 4045, 1.44, "clipboard retains source timing after timing changes; native=" + native);
		}

		foreach (int spacing in new[] { 8, 16, 12 })
		{
			var moving = fixture(); var before = snapshot(moving); divisor.SetArbitraryDivisor(spacing);
			clock.Seek(500); map.TimelineZoom = .3; composer.SetSelectTool(); yield return () => true;
			DateTime layout = DateTime.UtcNow.AddMilliseconds(200); yield return () => DateTime.UtcNow >= layout;
			double target = composer.SnapTime(1000 + 500d / spacing);
			Vector2 from = composer.Grid.ToScreenSpace(composer.Grid.PositionAt(1, 1000));
			Vector2 to = composer.Grid.ToScreenSpace(composer.Grid.PositionAt(1, target));
			new MousePositionAbsoluteInput { Position = from }.Apply(pointerInput.CurrentState, pointerInput); yield return () => true;
			new MouseButtonInput(MouseButton.Left, true).Apply(pointerInput.CurrentState, pointerInput); yield return () => true;
			new MousePositionAbsoluteInput { Position = from + (to - from).Normalized() * 30 }.Apply(pointerInput.CurrentState, pointerInput); yield return () => true;
			new MousePositionAbsoluteInput { Position = to }.Apply(pointerInput.CurrentState, pointerInput); yield return () => true;
			new MouseButtonInput(MouseButton.Left, false).Apply(pointerInput.CurrentState, pointerInput); yield return () => true;
			expect(map.HitObjects.Cast<ShredHitObject>(), before, target, 1, "fine mouse drag with 1/1 origin metadata, spacing=" + spacing);
			history.RestoreState(-1); yield return () => true;
			expect(map.HitObjects.Cast<ShredHitObject>(), before, 1000, 1, "drag undo");
		}

		// A real grid drag crosses the tempo boundary, then returns to its source.
		var dragBefore = snapshot(fixture()); divisor.SetArbitraryDivisor(16); clock.Seek(0); map.TimelineZoom = .08; composer.SetSelectTool(); yield return () => true;
		DateTime gridLayout = DateTime.UtcNow.AddMilliseconds(200); yield return () => DateTime.UtcNow >= gridLayout;
		Vector2 origin = composer.Grid.ToScreenSpace(composer.Grid.PositionAt(1, 1000));
		Vector2 far = composer.Grid.ToScreenSpace(composer.Grid.PositionAt(1, 4045));
		foreach (var step in drag58(origin, far)) yield return step;
		expect(map.HitObjects.Cast<ShredHitObject>(), dragBefore, 4045, 1.44, "grid drag across BPM changes");
		history.RestoreState(-1); yield return () => true;
		expect(map.HitObjects.Cast<ShredHitObject>(), dragBefore, 1000, 1, "cross-BPM drag undo");

		// Native timeline/nudge path: successive translations must preserve beats,
		// A real timeline drag also passes a stationary note mid-gesture.
		var timelineNotes = fixture(); var timelineBefore = snapshot(timelineNotes);
		var obstacle = new ShredHitObject { StartTime = 2600, Fret = 1, HeadSnapDivisor = 4 };
		map.BeginChange(); map.Add(obstacle); map.EndChange();
		map.SelectedHitObjects.Clear(); map.SelectedHitObjects.AddRange(timelineNotes);
		clock.Seek(2500); map.TimelineZoom = .65; yield return () => true;
		DateTime timelineLayout = DateTime.UtcNow.AddMilliseconds(250); yield return () => DateTime.UtcNow >= timelineLayout;
		var timeline = editor.ChildrenOfType<Timeline>().Single();
		var firstBlueprint = editor.ChildrenOfType<TimelineHitObjectBlueprint>().Single(b => b.Item == timelineNotes[0]);
		Vector2 timelineFrom = firstBlueprint.ToScreenSpace(new Vector2(0, firstBlueprint.DrawHeight / 2));
		Vector2 timelineTo = firstBlueprint.ToScreenSpace(new Vector2(timeline.PositionAtTime(4045) - timeline.PositionAtTime(1000), firstBlueprint.DrawHeight / 2));
		foreach (var step in drag58(timelineFrom, timelineTo)) yield return step;
		expect(map.HitObjects.Cast<ShredHitObject>().Where(n => n != obstacle), timelineBefore, 4045, 1.44, "real timeline drag retains rhythm and full LN through an intermediate overlap");
		check(map.HitObjects.Contains(obstacle) && close(obstacle.StartTime, 2600), "timeline drag keeps stationary note");
		history.RestoreState(-1); yield return () => true;
		check(map.HitObjects.Cast<ShredHitObject>().Any(n => close(n.StartTime, 1000) && close(n.Duration, 250)), "timeline drag undo restores the complete sustain");
		history.RestoreState(1); yield return () => true;
		check(map.HitObjects.Cast<ShredHitObject>().Any(n => close(n.StartTime, 4045) && close(n.Duration, 360)), "timeline drag redo restores the complete mapped sustain");

		// A real overlap at the destination is cut once, in the same undo/redo.
		timelineNotes = fixture();
		obstacle = new ShredHitObject { StartTime = 4200, Fret = 1, HeadSnapDivisor = 18 };
		map.BeginChange(); map.Add(obstacle); map.EndChange();
		map.SelectedHitObjects.Clear(); map.SelectedHitObjects.AddRange(timelineNotes);
		clock.Seek(2500); map.TimelineZoom = .65; yield return () => true;
		timelineLayout = DateTime.UtcNow.AddMilliseconds(250); yield return () => DateTime.UtcNow >= timelineLayout;
		firstBlueprint = editor.ChildrenOfType<TimelineHitObjectBlueprint>().Single(b => b.Item == timelineNotes[0]);
		timelineFrom = firstBlueprint.ToScreenSpace(new Vector2(0, firstBlueprint.DrawHeight / 2));
		timelineTo = firstBlueprint.ToScreenSpace(new Vector2(timeline.PositionAtTime(4045) - timeline.PositionAtTime(1000), firstBlueprint.DrawHeight / 2));
		foreach (var step in drag58(timelineFrom, timelineTo)) yield return step;
		check(map.HitObjects.Cast<ShredHitObject>().Any(n => n.Column == 1 && close(n.StartTime, 4045) && close(n.EndTime, 4200)), "timeline final overlap cuts at the destination note");
		history.RestoreState(-1); yield return () => true;
		check(map.HitObjects.Cast<ShredHitObject>().Any(n => n.Column == 1 && close(n.StartTime, 1000) && close(n.Duration, 250)), "timeline final overlap undo restores full original sustain in one step");
		history.RestoreState(1); yield return () => true;
		check(map.HitObjects.Cast<ShredHitObject>().Any(n => n.Column == 1 && close(n.StartTime, 4045) && close(n.EndTime, 4200)), "timeline final overlap redo restores the exact cut in one step");

		// Successive native translations must preserve beats,
		// without cumulative stretching when crossing the boundary and back.
		var nativeBefore = snapshot(fixture()); yield return () => true;
		foreach (double target in new[] { 4045d, 4405, 1062.5, 4045 })
		{
			double delta = target - map.HitObjects.Min(n => n.StartTime);
			map.BeginChange(); foreach (ShredHitObject n in map.HitObjects.ToArray()) { n.StartTime += delta; map.Update(n); } map.EndChange();
			yield return () => true;
			double ratio = 1; if (target >= 4000) ratio = 1.44;
			expect(map.HitObjects.Cast<ShredHitObject>(), nativeBefore, target, ratio, "native movement to " + target);
		}

		// Independent piecewise integration test: a sustain crosses a timing point
		// in both the source and the destination, with different tempo boundaries.
		var sourceTiming = new ControlPointInfo(); sourceTiming.Add(0, new TimingControlPoint { BeatLength = 500 }); sourceTiming.Add(2000, new TimingControlPoint { BeatLength = 250 });
		var destinationTiming = new ControlPointInfo(); destinationTiming.Add(0, new TimingControlPoint { BeatLength = 720 }); destinationTiming.Add(5000, new TimingControlPoint { BeatLength = 360 });
		var sourceBeats = new ShredBeatTimeline(sourceTiming);
		var pair = new[] { new ShredHitObject { StartTime = 1000, Fret = 1 }, new ShredHitObject { StartTime = 1750, Duration = 500, Fret = 2 } };
		check(ShredGroupTiming.Apply(pair.Select(n => (n, sourceBeats.Capture(n))), 4500, new ShredBeatTimeline(destinationTiming)), "piecewise group can be mapped");
		check(close(pair[0].StartTime, 4500) && close(pair[1].StartTime, 5270) && close(pair[1].EndTime, 5810), "source and destination BPM boundaries use the actual local quarter grid (v68)");

		var duplicateBefore = snapshot(fixture()); divisor.SetArbitraryDivisor(16); yield return () => true;
		container.ClipboardCommand(Key.D); yield return () => true;
		expect(map.SelectedHitObjects.Cast<ShredHitObject>(), duplicateBefore, 1531.25, 1, "Ctrl+D uses current spacing after the last note");
		history.RestoreState(-1); yield return () => true;

		// UI toggle off retains the stable v66.1 millisecond-translation behaviour.
		var offBefore = snapshot(fixture()); ui.SnapMovedToggle.Current.Value = false; clock.Seek(4045); yield return () => true;
		copy(true); paste(true); yield return () => true;
		expect(map.SelectedHitObjects.Cast<ShredHitObject>(), offBefore, 4045, 1, "toggle off preserves millisecond intervals across BPMs");
		ui.SnapMovedToggle.Current.Value = true;
		fixture(); clock.Seek(4045); yield return () => true; copy(true); paste(true); yield return () => true;
		var savedPositions = snapshot(map.HitObjects.Cast<ShredHitObject>());
		check((bool)typeof(Editor).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null)!, "v67 fixture saves");
		editor.Exit(); yield return () => ScreenStack.CurrentScreen is MainMenu;
		var saved = BeatmapManager.GetWorkingBeatmap(BeatmapManager.QueryBeatmap(b => b.ID == info.ID), refetch: true).GetPlayableBeatmap(new ShredRuleset().RulesetInfo);
		check(snapshot(saved.HitObjects.Cast<ShredHitObject>()).SequenceEqual(savedPositions), "saved musical paste reopens at exactly the same timestamps");
		ScreenStack.Push(new EditorLoader()); yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse;
		yield return () => true;
		check(((Editor)ScreenStack.CurrentScreen).ChildrenOfType<ShredHitObjectComposer>().Single().SnapState.SnapMovedObjects.Value, "group snap toggle persists across editor sessions");
	}
}
