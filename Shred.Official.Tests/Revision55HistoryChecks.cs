using osu.Framework.Allocation;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Localisation;
using osu.Game.Rulesets.Shred.Compatibility;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components.Menus;
using osuTK.Input;

internal static class Revision55HistoryChecks
{
	private static IEnumerable<MenuItem> flatten(IEnumerable<MenuItem> items)
	{
		foreach (MenuItem item in items)
		{
			yield return item;
			foreach (MenuItem child in flatten(item.Items)) yield return child;
		}
	}

	public static IEnumerable<Action> Steps(Editor editor)
	{
		var composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
		var map = composer.Dependencies.Get<EditorBeatmap>();
		var clock = composer.Dependencies.Get<EditorClock>();
		var history = (EditorChangeHandler)composer.Dependencies.Get<IEditorChangeHandler>();
		var session = composer.ChildrenOfType<ShredEditorSession>().Single();
		var menu = flatten(editor.ChildrenOfType<EditorMenuBar>().Single().Items).ToArray();
		var undo = menu.Single(i => i.Text.Value.Equals(CommonStrings.Undo));
		var redo = menu.Single(i => i.Text.Value.Equals(CommonStrings.Redo));
		int checks = 0;
		void check(bool ok, string label)
		{
			if (!ok) throw new Exception(label);
			checks++;
			Console.WriteLine("PASS v55 history " + label);
		}
		void position(double time, string label)
		{
			check(Math.Abs(clock.CurrentTimeAccurate - time) < 0.01, label + " leaves navigation target unchanged");
			check(Math.Abs(composer.Grid.CurrentTime - time) < 0.5, label + " leaves visible grid unchanged after update");
		}
		foreach (bool recorder in new[] { false, true })
		foreach (string operation in new[] { "move", "resize LN", "delete", "paste", "hitsound" })
		{
			string label = operation + ", recorder=" + recorder;
			string before = null, after = null;
			int selectedBefore = 0, selectedAfter = 0;
			yield return () =>
			{
				composer.StepRecorder.Enabled.Value = recorder;
				composer.SetSelectTool();
				clock.Stop();
				map.BeginChange();
				foreach (var note in map.HitObjects.ToArray()) map.Remove(note);
				map.Add(new ShredHitObject { StartTime = 2000, Fret = 1 });
				map.Add(new ShredHitObject { StartTime = 2500, Row = 1, Fret = 3, Duration = 750 });
				map.Add(new ShredHitObject { StartTime = 3000, Fret = 0 });
				map.Add(new ShredHitObject { StartTime = 3250, Articulation = ShredPracticeArticulation.Wham, Duration = 500 });
				map.EndChange();
				map.SelectedHitObjects.AddRange(map.HitObjects);
				session.FinishInputFrame();
			};
			yield return () =>
			{
				before = history.CurrentStateHash;
				selectedBefore = map.SelectedHitObjects.Count;
				map.BeginChange();
				if (operation == "paste")
				{
					var pasted = new ShredHitObject { StartTime = 4125, Row = 1, Fret = 2, Duration = 125 };
					map.Add(pasted);
					map.SelectedHitObjects.Clear();
					map.SelectedHitObjects.Add(pasted);
				}
				else foreach (var note in map.HitObjects.Cast<ShredHitObject>().ToArray())
				{
					if (operation == "delete") { map.Remove(note); continue; }
					if (operation == "move") note.StartTime += 1250;
					if (operation == "resize LN" && note.IsLong) note.Duration += 250;
					if (operation == "hitsound") note.Samples = new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_NORMAL, volume: 42) };
					map.Update(note);
				}
				map.EndChange();
				session.FinishInputFrame();
				after = history.CurrentStateHash;
				selectedAfter = map.SelectedHitObjects.Count;
				check(before != after, label + " produces a real content edit");
			};
			// Before, inside and after the affected objects, using the native menu actions.
			foreach (double time in new[] { 0d, 2250d, 10000d })
			{
				yield return () => clock.Seek(time);
				yield return () =>
				{
					undo.Action.Value.Invoke();
					check(history.CurrentStateHash == before && map.SelectedHitObjects.Count == selectedBefore, label + " undo restores content and selection");
					check(Math.Abs(clock.CurrentTimeAccurate - time) < 0.01, label + " undo does not seek at " + time);
				};
				yield return () => position(time, label + " undo at " + time);
				yield return () =>
				{
					redo.Action.Value.Invoke();
					check(history.CurrentStateHash == after && map.SelectedHitObjects.Count == selectedAfter, label + " redo restores content and selection");
					check(Math.Abs(clock.CurrentTimeAccurate - time) < 0.01, label + " redo does not seek at " + time);
				};
				yield return () => position(time, label + " redo at " + time);
			}
		}
		yield return () =>
		{
			map.SelectedHitObjects.Clear();
			session.FinishInputFrame();
			undo.Action.Value.Invoke();
			check(map.SelectedHitObjects.Count == 4, "undo accidental deselection restores group");
			redo.Action.Value.Invoke();
			check(map.SelectedHitObjects.Count == 0, "redo deselection clears group");
			position(10000, "selection-only undo and redo");
			// History must not cancel an already running navigation animation either.
			clock.SeekSmoothlyTo(10125);
			undo.Action.Value.Invoke();
			redo.Action.Value.Invoke();
			undo.Action.Value.Invoke();
			undo.Action.Value.Invoke();
			check(Math.Abs(clock.CurrentTimeAccurate - 10125) < 0.01, "content undo preserves pending smooth-scroll destination");
			redo.Action.Value.Invoke();
			check(Math.Abs(clock.CurrentTimeAccurate - 10125) < 0.01, "content redo preserves pending smooth-scroll destination");
		};
		yield return () =>
		{
			position(10125, "smooth scroll completes normally after history actions");
			clock.Seek(10437);
		};
		yield return () =>
		{
			string original = history.CurrentStateHash;
			void shortcut(Key key, bool shift = false)
			{
				var state = new InputState();
				state.Keyboard.Keys.SetPressed(Key.ControlLeft, true);
				if (shift) state.Keyboard.Keys.SetPressed(Key.ShiftLeft, true);
				var method = typeof(ShredStepRecorder).GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
				check((bool)method.Invoke(composer.StepRecorder, new object[] { new KeyDownEvent(state, key) }), "recorder handles " + key + ", shift=" + shift);
				check(Math.Abs(clock.CurrentTimeAccurate - 10437) < 0.01, "keyboard history preserves off-grid cursor");
			}
			shortcut(Key.Z);
			check(history.CurrentStateHash != original, "Ctrl+Z actually undoes edit");
			shortcut(Key.Y);
			check(history.CurrentStateHash == original, "Ctrl+Y actually redoes edit");
			shortcut(Key.Z);
			shortcut(Key.Z, shift: true);
			check(history.CurrentStateHash == original, "Ctrl+Shift+Z actually redoes edit");
		};
		yield return () =>
		{
			position(10437, "keyboard undo and redo after update");
			Console.WriteLine($"PASS v55 history: {checks} assertions in real official editor");
		};
	}
}
