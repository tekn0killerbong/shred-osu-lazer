using osu.Framework.Allocation;
using osu.Framework.Input.StateChanges;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osuTK.Input;

internal partial class Revision54EditorRateChecks
{
	private IEnumerable<Func<bool>> duplicateSteps(Editor editor)
	{
		editor.Mode.Value = EditorScreenMode.Compose;
		ShredHitObjectComposer composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
		EditorBeatmap map = editor.Dependencies.Get<EditorBeatmap>();
		EditorClock clock = editor.Dependencies.Get<EditorClock>();
		BindableBeatDivisor divisor = composer.Dependencies.Get<BindableBeatDivisor>();
		EditorChangeHandler history = (EditorChangeHandler)editor.Dependencies.Get<IEditorChangeHandler>();
		composer.Playback.ReturnOnPause.Value = false;
		DateTime entered = DateTime.UtcNow;
		yield return () => (DateTime.UtcNow - entered).TotalSeconds > 1;
		foreach (bool recorder in new[] { false, true })
		foreach (int spacing in new[] { 4, 6, 12 })
		foreach (string ending in new[] { "short", "LN", "LN chord", "short after LN", "short at LN end", "wham" })
		{
			composer.StepRecorder.Enabled.Value = recorder;
			divisor.SetArbitraryDivisor(spacing);
			clock.Stop(); clock.Seek(777.25);
			map.BeginChange();
			foreach (var note in map.HitObjects.ToArray()) map.Remove(note);
			foreach (ControlPointGroup group in map.ControlPointInfo.Groups.ToArray()) map.ControlPointInfo.RemoveGroup(group);
			map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
			map.ControlPointInfo.Add(2000, new TimingControlPoint { BeatLength = 400 });
			ShredHitObject[] originals = new[]
			{
				new ShredHitObject { StartTime = 1000, Fret = 2, Row = 0 },
				new ShredHitObject { StartTime = 1083.333333, Fret = 4, Row = 1, Articulation = ShredPracticeArticulation.HammerPull },
				new ShredHitObject { StartTime = 1500, Fret = 3, Row = 0 }
			};
			if (ending != "short") originals[0].Duration = 1100;
			if (ending == "LN chord") originals[1].Duration = 2100 - originals[1].StartTime;
			if (ending == "short after LN") originals[2].StartTime = 2300;
			if (ending == "short at LN end") originals[2].StartTime = 2100;
			if (ending == "wham") originals[0].Articulation = ShredPracticeArticulation.Wham;
			foreach (ShredHitObject note in originals) map.Add(note);
			map.EndChange();
			map.SelectedHitObjects.Clear(); map.SelectedHitObjects.AddRange(originals);
			composer.SetSelectTool();
			yield return () => true;
			// Capture after insertion has canonicalised historical decimal timings.
			double[] originalTimes = originals.Select(n => n.StartTime).ToArray();
			double[] originalEnds = originals.Select(n => n.EndTime).ToArray();
			double last = originals.Max(n => n.EndTime);
			double origin = 0;
			double beatLength = 500;
			if (last >= 2000) { origin = 2000; beatLength = 400; }
			double step = beatLength / spacing;
			double target = last;
			if (ending == "short" || ending == "short after LN" || ending == "short at LN end")
				target = origin + (Math.Floor((last - origin) / step + 0.000001) + 1) * step;
			new KeyboardKeyInput(Key.ControlLeft, true).Apply(pointerInput.CurrentState, pointerInput);
			new KeyboardKeyInput(Key.D, true).Apply(pointerInput.CurrentState, pointerInput);
			new KeyboardKeyInput(Key.D, false).Apply(pointerInput.CurrentState, pointerInput);
			new KeyboardKeyInput(Key.ControlLeft, false).Apply(pointerInput.CurrentState, pointerInput);
			yield return () => true;
			string label = $"Ctrl+D spacing 1/{spacing}, recorder={recorder}, ending={ending}";
			ShredHitObject[] copies = map.HitObjects.Cast<ShredHitObject>().Where(n => !originals.Contains(n)).OrderBy(n => n.StartTime).ToArray();
			check(copies.Length == 3 && close(copies[0].StartTime, target), label + " starts at the expected boundary; expected=" + target
				+ "; copies=" + string.Join(",", copies.Select(n => n.StartTime)) + "; selected=" + map.SelectedHitObjects.Count + "; divisor=" + divisor.Value);
			check(copies.Zip(originals).All(pair => close(pair.First.StartTime - copies[0].StartTime, pair.Second.StartTime - 1000)
				&& Math.Abs(pair.First.Duration - pair.Second.Duration) < .000002 && pair.First.Row == pair.Second.Row && pair.First.Fret == pair.Second.Fret
				&& pair.First.Articulation == pair.Second.Articulation), label + " preserves mixed-grid spacing, LN and types");
			check(originals.Select(n => n.StartTime).SequenceEqual(originalTimes) && originals.Select(n => n.EndTime).SequenceEqual(originalEnds) && close(clock.CurrentTimeAccurate, 777.25),
				label + " keeps originals and viewport");
			check(map.SelectedHitObjects.Count == 3 && map.SelectedHitObjects.All(n => copies.Contains(n)), label + " selects the copy");
			history.RestoreState(-1);
			yield return () => true;
			check(map.HitObjects.Count == 3 && map.SelectedHitObjects.Count == 3, label + " one undo restores original selection");
			history.RestoreState(1);
			yield return () => true;
			check(map.HitObjects.Count == 6 && map.SelectedHitObjects.Count == 3 && close(clock.CurrentTimeAccurate, 777.25), label + " redo restores copy without seek");
		}
	}
}
