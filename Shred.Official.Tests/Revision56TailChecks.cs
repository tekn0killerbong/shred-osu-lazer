using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Input;
using osu.Framework.Input.StateChanges;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osuTK.Input;

internal partial class Revision54EditorRateChecks
{
	private IEnumerable<Func<bool>> tailInsertionSteps(Editor editor)
	{
		ShredHitObjectComposer composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
		EditorBeatmap map = editor.Dependencies.Get<EditorBeatmap>();
		EditorClock clock = editor.Dependencies.Get<EditorClock>();
		EditorChangeHandler history = (EditorChangeHandler)editor.Dependencies.Get<IEditorChangeHandler>();
		BindableBeatDivisor divisor = composer.Dependencies.Get<BindableBeatDivisor>();
		void shortcut(Key key)
		{
			new KeyboardKeyInput(Key.ControlLeft, true).Apply(pointerInput.CurrentState, pointerInput);
			new KeyboardKeyInput(key, true).Apply(pointerInput.CurrentState, pointerInput);
			new KeyboardKeyInput(key, false).Apply(pointerInput.CurrentState, pointerInput);
			new KeyboardKeyInput(Key.ControlLeft, false).Apply(pointerInput.CurrentState, pointerInput);
		}
		foreach (string operation in new[] { "placement", "paste recorder off", "paste recorder on", "menu paste" })
		foreach (double at in new[] { 1500d, 2000d })
		foreach (double duration in new[] { 0d, 250d, 1200d })
		{
			composer.StepRecorder.Enabled.Value = false;
			((IFocusManager)typeof(osu.Framework.Graphics.Drawable)
				.GetMethod("GetContainingFocusManager", BindingFlags.Instance | BindingFlags.NonPublic)!
				.Invoke(composer.StepRecorder, null)!).ChangeFocus(null);
			composer.SetSelectTool();
			clock.Stop(); clock.Seek(at);
			divisor.SetArbitraryDivisor(4);
			map.BeginChange();
			foreach (var note in map.HitObjects.ToArray()) map.Remove(note);
			foreach (ControlPointGroup group in map.ControlPointInfo.Groups.ToArray()) map.ControlPointInfo.RemoveGroup(group);
			map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
			ShredHitObject tail = new ShredHitObject { StartTime = 1000, Duration = 1000, Fret = 1 };
			ShredHitObject neighbour = new ShredHitObject { StartTime = 1000, Duration = 2000, Fret = 2 };
			ShredHitObject otherRow = new ShredHitObject { StartTime = 1000, Duration = 2000, Fret = 1, Row = 1 };
			ShredHitObject source = new ShredHitObject { StartTime = 5000, Duration = duration, Fret = 1 };
			ShredHitObject future = new ShredHitObject { StartTime = 2250, Fret = 1 };
			foreach (ShredHitObject note in new[] { tail, neighbour, otherRow, source, future }) map.Add(note);
			map.EndChange();
			map.SelectedHitObjects.Clear(); map.SelectedHitObjects.Add(source);
			if (operation == "paste recorder on") composer.StepRecorder.Enabled.Value = true;
			yield return () => true;
			if (operation == "menu paste")
				typeof(Editor).GetMethod("Copy", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null);
			else if (operation != "placement") shortcut(Key.C);
			yield return () => true;
			if (operation == "placement")
			{
				ShredHitObject note = new ShredHitObject { StartTime = at, Duration = duration, Fret = 1 };
				composer.BeginPlacementChange(note);
				map.Add(note);
				composer.EndPlacementChange();
			}
			else if (operation == "menu paste")
				typeof(Editor).GetMethod("Paste", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null);
			else shortcut(Key.V);
			yield return () => true;
			string label = operation + " at=" + at + ", inserted duration=" + duration;
			double expectedDuration = Math.Min(duration, future.StartTime - at);
			check(map.HitObjects.Count == 6 && map.HitObjects.Cast<ShredHitObject>().Any(n => n.StartTime == at && n.Fret == 1 && n.Row == 0 && n.Duration == expectedDuration),
				label + " creates the object, stopping its own tail at the next existing head if needed");
			check(tail.EndTime == at && neighbour.EndTime == 3000 && otherRow.EndTime == 3000,
				label + " cuts only the overlapped LN exactly at new head; actual end=" + tail.EndTime);
			history.RestoreState(-1);
			yield return () => true;
			check(map.HitObjects.Count == 5 && map.HitObjects.Cast<ShredHitObject>().Single(n => n.StartTime == 1000 && n.Row == 0 && n.Fret == 1).EndTime == 2000,
				label + " one undo restores the entire original LN");
			history.RestoreState(1);
			yield return () => true;
			check(map.HitObjects.Count == 6 && map.HitObjects.Cast<ShredHitObject>().Single(n => n.StartTime == 1000 && n.Row == 0 && n.Fret == 1).EndTime == at
				&& map.HitObjects.Cast<ShredHitObject>().Single(n => n.StartTime == at && n.Row == 0 && n.Fret == 1).Duration == expectedDuration
				&& close(clock.CurrentTimeAccurate, at), label + " redo restores exact cut without moving grid; count=" + map.HitObjects.Count
				+ ", end=" + map.HitObjects.Cast<ShredHitObject>().Single(n => n.StartTime == 1000 && n.Row == 0 && n.Fret == 1).EndTime
				+ ", cursor=" + clock.CurrentTimeAccurate);
		}
		composer.StepRecorder.Enabled.Value = false;
	}
}
