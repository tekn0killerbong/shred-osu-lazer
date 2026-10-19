using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.StateChanges;
using osu.Framework.Testing;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osuTK.Input;

internal partial class Revision54EditorRateChecks
{
	private IEnumerable<Func<bool>> redoShortcutSteps(Editor editor)
	{
		ShredHitObjectComposer composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
		EditorBeatmap map = editor.Dependencies.Get<EditorBeatmap>();
		EditorClock clock = editor.Dependencies.Get<EditorClock>();
		void key(Key value, bool down) => new KeyboardKeyInput(value, down).Apply(pointerInput.CurrentState, pointerInput);
		void shortcut(Key value, bool shift = false)
		{
			key(Key.ControlLeft, true);
			if (shift) key(Key.ShiftLeft, true);
			key(value, true); key(value, false);
			if (shift) key(Key.ShiftLeft, false);
			key(Key.ControlLeft, false);
		}
		foreach (bool enabled in new[] { false, true })
		foreach (Key redo in new[] { Key.Y, Key.Z })
		{
			composer.StepRecorder.Enabled.Value = enabled;
			composer.SetSelectTool();
			clock.Stop(); clock.Seek(777.25);
			if (!enabled)
				((IFocusManager)typeof(Drawable).GetMethod("GetContainingFocusManager", BindingFlags.Instance | BindingFlags.NonPublic)!
					.Invoke(composer.StepRecorder, null)!).ChangeFocus(null);
			map.BeginChange();
			foreach (var old in map.HitObjects.ToArray()) map.Remove(old);
			ShredHitObject note = new ShredHitObject { StartTime = 1000, Fret = 1 };
			map.Add(note);
			map.EndChange();
			map.SelectedHitObjects.Clear(); map.SelectedHitObjects.Add(note);
			yield return () => !enabled || composer.StepRecorder.HasFocus;
			foreach (double duration in new[] { 125d, 250d })
			{
				map.BeginChange(); note.Duration = duration; map.Update(note); map.EndChange();
				yield return () => true;
			}
			string label = "redo=" + redo + ", recorder=" + enabled;
			shortcut(Key.Z);
			yield return () => true;
			check(((ShredHitObject)map.HitObjects.Single()).Duration == 125, label + " Ctrl+Z undoes one edit");
			shortcut(Key.Z);
			yield return () => true;
			check(((ShredHitObject)map.HitObjects.Single()).Duration == 0, label + " second Ctrl+Z undoes previous edit");
			foreach (double duration in new[] { 125d, 250d })
			{
				shortcut(redo, redo == Key.Z);
				yield return () => true;
				check(((ShredHitObject)map.HitObjects.Single()).Duration == duration,
					label + " redoes exactly one edit to duration " + duration);
				check(map.SelectedHitObjects.Count == 1 && map.SelectedHitObjects.Single() == map.HitObjects.Single()
					&& close(clock.CurrentTimeAccurate, 777.25), label + " preserves selection and grid position");
			}
		}
		composer.StepRecorder.Enabled.Value = false;
	}
}
