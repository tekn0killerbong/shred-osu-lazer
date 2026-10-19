using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Game.Audio;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK.Input;

namespace osu.Game.Rulesets.Shred.Edit
{
	/// <summary>Focused raw-key routing has priority over the editor's action containers.</summary>
	public partial class ShredStepRecorder : Container
	{
		[Resolved] private GameHost host { get; set; }
		[Resolved] private EditorClock editorClock { get; set; }
		[Resolved] private EditorBeatmap beatmap { get; set; }
		[Resolved] private IEditorChangeHandler changeHandler { get; set; }
		[Resolved] private BindableBeatDivisor divisor { get; set; }
		[Resolved] private ShredHitObjectComposer composer { get; set; }
		public readonly BindableBool Enabled = new BindableBool();
		private readonly Stopwatch realClock = Stopwatch.StartNew();
		private readonly Dictionary<ShredStepRecorderState.Note, ShredHitObject> objects = new Dictionary<ShredStepRecorderState.Note, ShredHitObject>();
		private readonly Dictionary<ShredHitObject, double> preservedDurations = new Dictionary<ShredHitObject, double>();
		private readonly Dictionary<Key, ShredAction[]> physicalKeys = new Dictionary<Key, ShredAction[]>();
		private readonly HashSet<Key> consumedKeys = new HashSet<Key>();
		private readonly Dictionary<ShredAction, int> pressed = new Dictionary<ShredAction, int>();
		private readonly Dictionary<ShredAction, int> actionCounts = new Dictionary<ShredAction, int>();
		private readonly ShredRecorderBindingSource<ShredAction> gameplayBindings;
		private readonly ShredRecorderBindingSource<ShredEditorAction> editorBindings;
		private BeatDivisorPresetCollection customDivisors;
		private ShredStepRecorderState recorder;
		private bool changing;
		public override bool AcceptsFocus => true;
		// Request vacant focus, never steal it from a text box or modal window.
		public override bool RequestsFocus => Enabled.Value && host?.IsActive.Value == true;

		public ShredStepRecorder(RulesetInfo ruleset)
		{
			RelativeSizeAxes = Axes.Both;
			Add(gameplayBindings = new ShredRecorderBindingSource<ShredAction>(ruleset, 0));
			Add(editorBindings = new ShredRecorderBindingSource<ShredEditorAction>(ruleset, Ruleset.EDITOR_VARIANT));
		}
		protected override void LoadComplete()
		{
			base.LoadComplete();
			Enabled.BindValueChanged(value =>
			{
				finishGesture();
				if (!value.NewValue) return;
				editorClock.Stop();
				editorClock.Seek(Math.Max(0, composer.SnapTime(editorClock.CurrentTime)));
				startAtCursor();
				Scheduler.Add(() => this.GetContainingFocusManager().ChangeFocus(this));
			});
		}
		private void startAtCursor()
        {
            double position = Math.Max(0, composer.SnapTime(editorClock.CurrentTimeAccurate));
            editorClock.Seek(position);
            recorder = new ShredStepRecorderState(position, nextStep, addNote, updateNote, time => editorClock.Seek(time));
        }
		private double nextStep(double position)
			=> Math.Min(editorClock.TrackLength, composer.Grid.NextStep(position));

		private void beginChange()
		{
			if (changing) return;
			beatmap.BeginChange();
			changing = true;
		}
		private void endChange()
		{
			if (!changing) return;
			double cursor = recorder?.Position ?? editorClock.CurrentTimeAccurate;
			beatmap.EndChange();
			// Committing a short-only gesture must not rewind its completed step.
			editorClock.Seek(cursor);
			changing = false;
			objects.Clear();
			preservedDurations.Clear();
		}
		private static ShredPracticeArticulation articulation(ShredStepRecorderState.Note entry)
		{
			if (entry.Column == 12) return ShredPracticeArticulation.Wham;
			if (entry.Picked || entry.Column % 6 == 0) return ShredPracticeArticulation.Pick;
			return ShredPracticeArticulation.HammerPull;
		}
		private void addNote(ShredStepRecorderState.Note entry)
		{
			beginChange();
			var note = new ShredHitObject
			{
				StartTime = entry.Start, Articulation = articulation(entry),
				Samples = new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) }
			};
			if (note.IsWham) note.Duration = Math.Max(1, nextStep(entry.Start) - entry.Start);
			else note.X = entry.Column;
			composer.TrimSustainsAt(note);
			ShredHitObject[] coincident = beatmap.HitObjects.OfType<ShredHitObject>()
				.Where(n => n.IsWham == note.IsWham && (note.IsWham || n.Column == note.Column)
					&& Math.Abs(n.StartTime - note.StartTime) < 0.5).ToArray();
			if (coincident.Length > 0)
			{
				ShredHitObject existing = coincident.OrderByDescending(n => n.Duration).First();
				foreach (ShredHitObject duplicate in coincident.Where(n => n != existing)) beatmap.Remove(duplicate);
				if (note.Articulation == ShredPracticeArticulation.Pick) existing.Articulation = note.Articulation;
				note = existing;
				preservedDurations[note] = note.Duration;
				// Detach replaced recorder entries so a later pick cannot revive them.
				foreach (var stale in objects.Where(pair => coincident.Contains(pair.Value)).Select(pair => pair.Key).ToArray())
					objects.Remove(stale);
				objects[entry] = note;
				note.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
				beatmap.Update(note);
			}
			else
			{
				objects[entry] = note;
				note.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
				beatmap.Add(note);
			}
		}
		private void updateNote(ShredStepRecorderState.Note entry)
		{
			if (!objects.TryGetValue(entry, out ShredHitObject note)) return;
			if (note.Articulation != ShredPracticeArticulation.Pick || entry.Picked)
				note.Articulation = articulation(entry);
			preservedDurations.TryGetValue(note, out double minimumDuration);
			if (entry.Sustain) note.Duration = Math.Max(minimumDuration, entry.End - entry.Start);
			if (note.IsWham) note.Duration = Math.Max(1, note.Duration);
			note.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
			beatmap.Update(note);
		}
		private void press(ShredAction action, double now)
		{
			if (pressed.ContainsKey(action)) return;
			if (action == ShredAction.Wham)
			{
				pressed[action] = 12;
				recorder.Press(12, now, true);
				return;
			}
			if (!ShredBindingProfile.TryDecode(action, out int row, out int index)) return;
			if (index < 5)
			{
				composer.Grid.FocusRow(row);
				int column = row * 6 + index + 1;
				pressed[action] = column;
				bool picked = pressed.Keys.Any(a => ShredBindingProfile.TryDecode(a, out int r, out int i) && r == row && i >= 5);
				recorder.Press(column, now, picked);
				composer.Grid.SetFretPressed(row, index, true);
				return;
			}
			composer.Grid.FocusRow(row);
			int[] frets = Enumerable.Range(row * 6 + 1, 5).Where(pressed.Values.Contains).ToArray();
			pressed[action] = -1;
			if (frets.Length == 0)
			{
				pressed[action] = row * 6;
				recorder.Pick(new[] { row * 6 }, now);
			}
			else recorder.Pick(Enumerable.Range(row * 6 + 1, 5), now);
		}
		private void release(ShredAction action, double now)
		{
			if (!pressed.TryGetValue(action, out int column)) return;
			pressed.Remove(action);
			if (ShredBindingProfile.TryDecode(action, out int row, out int index) && index < 5)
				composer.Grid.SetFretPressed(row, index, false);
			if (column >= 0 && !pressed.Values.Contains(column)) recorder.Release(column, now);
			if (!recorder.HasHeldNotes) endChange();
		}
		private static IEnumerable<IKeyBinding> matching(IReadOnlyList<IKeyBinding> bindings, KeyDownEvent e)
		{
			InputKey key = KeyCombination.FromKey(e.Key);
			var down = KeyCombination.FromInputState(e.CurrentState, null).Keys;
			return bindings.Where(b => b.KeyCombination.Keys.Contains(key) && b.KeyCombination.Keys.All(down.Contains))
				.OrderByDescending(b => b.KeyCombination.Keys.Count());
		}
		protected override bool OnKeyDown(KeyDownEvent e)
		{
            // This binding remains reachable with the recorder disabled.
            IKeyBinding toggle = matching(editorBindings.Mappings, e)
                .FirstOrDefault(b => Convert.ToInt32(b.Action) == (int)ShredEditorAction.ToggleStepRecorder);
            if (toggle != null)
            {
                if (!e.Repeat) Enabled.Value = !Enabled.Value;
                return true;
            }
            if (e.ControlPressed && e.Key == Key.K && !e.AltPressed && !e.SuperPressed)
            {
                if (!e.Repeat)
                {
                    finishGesture();
                    ((ShredSelectionHandler)composer.BlueprintContainer.SelectionHandler).InvertSelection();
                }
                return true;
            }
            if (e.ControlPressed && !e.AltPressed && !e.SuperPressed
                && (e.Key == Key.C || e.Key == Key.V || e.Key == Key.D || e.Key == Key.X))
            {
                if (!e.Repeat)
                {
                    finishGesture();
                    ((ShredBlueprintContainer)composer.BlueprintContainer).ClipboardCommand(e.Key);
                }
                return true;
            }
			if (!Enabled.Value || !HasFocus) return false;
			// Editor commands must precede gameplay matching (Z is a default fret).
			if (e.ControlPressed && !e.AltPressed && !e.SuperPressed
				&& (e.Key == Key.Z || e.Key == Key.Y || e.Key == Key.S))
			{
				if (e.Key == Key.S)
				{
					if (!e.Repeat) finishGesture();
					return false;
				}
				if (!e.Repeat)
				{
					finishGesture();
					int direction = -1;
					if (e.ShiftPressed || e.Key == Key.Y) direction = 1;
					changeHandler.RestoreState(direction);
					startAtCursor();
				}
				return true;
			}
			if (e.Key == Key.Space && !e.AltPressed && !e.SuperPressed && !e.ShiftPressed)
			{
				if (!e.Repeat)
				{
					finishGesture();
					composer.Playback.Toggle(!e.ControlPressed);
				}
				return true;
			}
			if ((e.Key == Key.Left || e.Key == Key.Right) && !e.ControlPressed && !e.AltPressed && !e.SuperPressed && !e.ShiftPressed)
			{
                composer.Playback.NotifyNavigation();
                double position = editorClock.CurrentTimeAccurate;
                double target = nextStep(position);
                if (e.Key == Key.Left)
                {
                    double step = beatmap.ControlPointInfo.TimingPointAt(position).BeatLength / divisor.Value;
                    target = ShredGridTicks.Enumerate(beatmap.ControlPointInfo, divisor.Value,
                        Math.Max(0, position - step * 4 - 1), position - 0.001)
                        .Select(t => t.Time).DefaultIfEmpty(0).Last();
                }
                if (!editorClock.IsRunning)
                {
                    if (recorder == null) startAtCursor();
                    recorder.Navigate(Math.Max(0, target), realClock.Elapsed.TotalMilliseconds);
                }
                else
                {
                    finishGesture();
                    editorClock.Seek(Math.Max(0, target));
                }
				return true;
			}
            // Modified editor commands must never fall through to bare J, frets, etc.
            // Let lazer dispatch its native actions (save, flip, select all, ...).
            if (e.ControlPressed || e.AltPressed || e.SuperPressed
                || (e.ShiftPressed && e.Key != Key.ShiftLeft && e.Key != Key.ShiftRight
                    && !physicalKeys.ContainsKey(Key.ShiftLeft) && !physicalKeys.ContainsKey(Key.ShiftRight)))
            {
                if (!e.Repeat) finishGesture();
                return false;
            }
			if (editorClock.IsRunning) return false;
            if (recorder == null || (!recorder.HasHeldNotes && Math.Abs(editorClock.CurrentTimeAccurate - recorder.Position) > 0.5))
            {
                finishGesture();
                startAtCursor();
            }
			if (e.Key == Key.Escape) { Enabled.Value = false; return true; }
			IKeyBinding editor = matching(editorBindings.Mappings, e).FirstOrDefault(b => Convert.ToInt32(b.Action) >= 2);
			if (editor != null)
			{
                if (e.Repeat || !consumedKeys.Add(e.Key)) return true;
				double now = realClock.Elapsed.TotalMilliseconds;
				switch ((ShredEditorAction)Convert.ToInt32(editor.Action))
				{
					case ShredEditorAction.EmptyStep:
					case ShredEditorAction.EmptyStepAlternate: recorder.EmptyStep(now); break;
					case ShredEditorAction.CycleGridSpacing:
						int old = divisor.Value;
						divisor.SelectNext();
						if (divisor.Value == old) divisor.Value = divisor.ValidDivisors.Value.Presets.First();
						break;
					case ShredEditorAction.CycleGridType: cycleMode(); break;
				}
				return true;
			}
			ShredAction[] actions = matching(gameplayBindings.Mappings, e)
				.Select(b => (ShredAction)Convert.ToInt32(b.Action))
				.Where(a => a == ShredAction.Wham || ShredBindingProfile.TryDecode(a, out _, out _)).Distinct().ToArray();
			if (actions.Length > 0)
			{
                if (e.Repeat || !consumedKeys.Add(e.Key)) return true;
				physicalKeys[e.Key] = actions;
				foreach (ShredAction action in actions)
				{
					actionCounts.TryGetValue(action, out int count);
					actionCounts[action] = count + 1;
					if (count == 0) press(action, realClock.Elapsed.TotalMilliseconds);
				}
				return true;
			}
			// Unassigned keys retain their normal editor action.
			return false;
		}
		protected override void OnKeyUp(KeyUpEvent e)
		{
			if (e.Key == Key.ControlLeft || e.Key == Key.ControlRight)
				((ShredBlueprintContainer)composer.BlueprintContainer).ResetControlAnchor();
			consumedKeys.Remove(e.Key);
			if (!physicalKeys.Remove(e.Key, out ShredAction[] actions)) return;
			foreach (ShredAction action in actions)
			{
				if (--actionCounts[action] > 0) continue;
				actionCounts.Remove(action);
				release(action, realClock.Elapsed.TotalMilliseconds);
			}
		}
		private void cycleMode()
		{
			BeatDivisorPresetCollection current = divisor.ValidDivisors.Value;
			if (current.Type == BeatDivisorType.Custom) customDivisors = current;
			var modes = new List<BeatDivisorPresetCollection> { BeatDivisorPresetCollection.COMMON, BeatDivisorPresetCollection.TRIPLETS };
			if (customDivisors != null) modes.Add(customDivisors);
			int index = modes.FindIndex(mode => mode.Type == current.Type);
			divisor.ValidDivisors.Value = modes[(index + 1) % modes.Count];
		}
		protected override void OnFocusLost(FocusLostEvent e)
		{
			finishGesture();
			base.OnFocusLost(e);
		}
        protected override bool OnScroll(ScrollEvent e)
        {
            if (Enabled.Value && !editorClock.IsRunning) finishGesture();
            return false;
        }
		protected override bool OnMouseDown(MouseDownEvent e)
		{
            if (e.Button == MouseButton.Left && composer.Grid.ContainsScreen(e.ScreenSpaceMousePosition))
                composer.Grid.FocusRow(composer.Grid.RowAt(e.ScreenSpaceMousePosition));
            if (Enabled.Value && !editorClock.IsRunning) finishGesture();
			if (Enabled.Value && composer.Grid.ContainsScreen(e.ScreenSpaceMousePosition))
				Scheduler.Add(() => this.GetContainingFocusManager().ChangeFocus(this));
			return false;
		}
		protected override void Update()
		{
			base.Update();
			if (!Enabled.Value) return;
			if (!host.IsActive.Value || editorClock.IsRunning || !HasFocus)
			{
				if (recorder != null) finishGesture();
				return;
			}
			if (recorder == null)
			{
				finishGesture();
				startAtCursor();
			}
			recorder.Tick(realClock.Elapsed.TotalMilliseconds);
		}
		private void finishGesture()
		{
			endChange();
			recorder?.Finish();
			recorder = null;
			pressed.Clear(); physicalKeys.Clear(); actionCounts.Clear(); consumedKeys.Clear();
			if (composer != null) composer.Grid.ResetFretPresses();
			endChange();
		}
		protected override void Dispose(bool isDisposing)
		{
			if (isDisposing) finishGesture();
			base.Dispose(isDisposing);
		}
	}
}
