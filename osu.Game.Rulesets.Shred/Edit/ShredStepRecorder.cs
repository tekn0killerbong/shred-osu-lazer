using System;
using System.Collections.Generic;
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
	public partial class ShredStepRecorder : Container, IKeyBindingHandler<ShredEditorAction>
	{
		[Resolved] private GameHost host { get; set; }
		[Resolved] private EditorClock editorClock { get; set; }
		[Resolved] private EditorBeatmap beatmap { get; set; }
		[Resolved] private IEditorChangeHandler changeHandler { get; set; }
		[Resolved] private BindableBeatDivisor divisor { get; set; }
		[Resolved] private ShredHitObjectComposer composer { get; set; }
		public readonly BindableBool Enabled = new BindableBool();
		private readonly Dictionary<ShredStepRecorderState.Note, ShredHitObject> objects = new Dictionary<ShredStepRecorderState.Note, ShredHitObject>();
		private readonly Dictionary<Key, ShredAction[]> physicalKeys = new Dictionary<Key, ShredAction[]>();
		private readonly HashSet<Key> consumedKeys = new HashSet<Key>();
		private readonly Dictionary<ShredAction, int> pressed = new Dictionary<ShredAction, int>();
		private readonly Dictionary<ShredAction, int> actionCounts = new Dictionary<ShredAction, int>();
		private readonly ShredRecorderBindingSource<ShredAction> gameplayBindings;
		private readonly ShredRecorderBindingSource<ShredEditorAction> editorBindings;
		private BeatDivisorPresetCollection customDivisors;
		private ShredStepRecorderState recorder;
		private ShredEditorGridMemory gridMemory;
		private bool changing;
		private ShredPracticeArticulation? activeTool;
		public override bool AcceptsFocus => true;
		// Request vacant focus, never steal it from a text box or modal window.
		public override bool RequestsFocus => Enabled.Value && host?.IsActive.Value == true;

		public ShredStepRecorder(RulesetInfo ruleset)
		{
			RelativeSizeAxes = Axes.Both;
			Add(gameplayBindings = new ShredRecorderBindingSource<ShredAction>(ruleset, 0));
			Add(editorBindings = new ShredRecorderBindingSource<ShredEditorAction>(ruleset, Ruleset.EDITOR_VARIANT));
		}
        private ShredRecorderConfig preferences;
        private void loadPreferences()
        {
            preferences = new ShredRecorderConfig(host.Storage);
            preferences.BindWith(ShredRecorderSetting.ReturnOnPause, composer.Playback.ReturnOnPause);
            preferences.BindWith(ShredRecorderSetting.Enabled, Enabled);
			preferences.BindWith(ShredRecorderSetting.SnapMovedObjects, composer.SnapState.SnapMovedObjects);
        }
		protected override void LoadComplete()
		{
			base.LoadComplete();
			// Restore before installing the handler that defaults deliberate type changes to 1/4 or 1/6.
			gridMemory = new ShredEditorGridMemory(host.Storage, beatmap.BeatmapInfo.ID, divisor, action => Scheduler.Add(action));
            divisor.ValidDivisors.BindValueChanged(v =>
            {
                if (v.NewValue.Type == BeatDivisorType.Custom) customDivisors = v.NewValue;
                if (v.NewValue.Type != v.OldValue.Type)
                {
                    int initial = v.NewValue.Type == BeatDivisorType.Common ? 4
                        : v.NewValue.Type == BeatDivisorType.Triplets ? 6 : 0;
                    if (initial != 0)
                    {
                        divisor.Value = initial;
                        // The sidebar owns another bound copy. Its old preset can
                        // reject this value while the preset change is propagating.
                        // Retry after propagation, unless an explicit spacing was chosen.
                        Scheduler.Add(() =>
                        {
                            if (divisor.ValidDivisors.Value.Type == v.NewValue.Type && divisor.Value == 1)
                                divisor.Value = initial;
                        });
                    }
                }
            }, true);
            Enabled.BindValueChanged(value =>
			{
				finishGesture();
				if (!value.NewValue) return;
				editorClock.Stop();
				editorClock.Seek(Math.Max(0, composer.SnapTime(editorClock.CurrentTime)));
				startAtCursor();
				Scheduler.Add(() => this.GetContainingFocusManager().ChangeFocus(this));
			});
            loadPreferences();
		}
        public static bool IsMouseToolKey(Key key) => key == Key.Number1 || key == Key.Number2 || key == Key.Number3;

		private void startAtCursor(bool snap = false)
        {
            double position = editorClock.CurrentTimeAccurate;
            if (snap) position = composer.SnapTime(position);
            position = Math.Max(0, position);
            if (Math.Abs(editorClock.CurrentTimeAccurate - position) > 0.001) editorClock.SeekSmoothlyTo(position);
            recorder = new ShredStepRecorderState(position, nextStep, addNote, updateNote, time => editorClock.SeekSmoothlyTo(time));
			activeTool = composer.RecorderArticulation;
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
			double cursor = editorClock.CurrentTimeAccurate;
            // TrimSustainsAt already cuts an overlapped tail at the new head.
            // Do not apply the mouse editor's former one-step chord gap here.
			beatmap.EndChange();
			// Committing a short-only gesture must not rewind its completed step.
			if (Math.Abs(editorClock.CurrentTimeAccurate - cursor) > 0.001) editorClock.SeekSmoothlyTo(cursor);
			changing = false;
			objects.Clear();
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
				HeadSnapDivisor = divisor.Value, TailSnapDivisor = divisor.Value,
				Samples = composer.CreateRecorderSamples(entry.Start)
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
				existing.Articulation = note.Articulation;
				note = existing;
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
			if (entry.Sustain) note.Duration = Math.Max(0, entry.End - entry.Start);
			if (entry.Sustain) note.TailSnapDivisor = divisor.Value;
			if (note.IsWham) note.Duration = Math.Max(1, note.Duration);
			note.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
			beatmap.Update(note);
		}
		private void press(ShredAction action)
		{
            if (pressed.ContainsKey(action) || !composer.RecorderArticulation.HasValue) return;
            if (action == ShredAction.Wham)
			{
				pressed[action] = 12;
				recorder.Press(12, true);
				return;
			}
			if (!ShredBindingProfile.TryDecode(action, out int row, out int index)) return;
			if (index < 5)
			{
				composer.Grid.FocusRow(row);
				int column = row * 6 + index + 1;
				pressed[action] = column;
				bool picked = composer.RecorderArticulation == ShredPracticeArticulation.Pick;
				recorder.Press(column, picked);
				composer.Grid.SetFretPressed(row, index, true);
				return;
			}
			// Fingerpicks never create OPEN notes or modify a recorded chord.
		}
		private void release(ShredAction action)
		{
			if (!pressed.TryGetValue(action, out int column)) return;
			pressed.Remove(action);
			if (ShredBindingProfile.TryDecode(action, out int row, out int index) && index < 5)
				composer.Grid.SetFretPressed(row, index, false);
			if (column >= 0 && !pressed.Values.Contains(column)) recorder.Release(column);
			if (!recorder.HasHeldInputs) endChange();
		}
		private static IEnumerable<IKeyBinding> matching(IReadOnlyList<IKeyBinding> bindings, KeyDownEvent e)
		{
			InputKey key = KeyCombination.FromKey(e.Key);
			var down = KeyCombination.FromInputState(e.CurrentState, null).Keys;
			return bindings.Where(b => b.KeyCombination.Keys.Contains(key)
                && (b.KeyCombination.Keys.Any(k => k == InputKey.Control || k == KeyCombination.FromKey(Key.ControlLeft) || k == KeyCombination.FromKey(Key.ControlRight)) == e.ControlPressed)
                && (b.KeyCombination.Keys.Any(k => k == InputKey.Shift || k == KeyCombination.FromKey(Key.ShiftLeft) || k == KeyCombination.FromKey(Key.ShiftRight)) == e.ShiftPressed)
                && (b.KeyCombination.Keys.Any(k => k == InputKey.Alt || k == KeyCombination.FromKey(Key.AltLeft) || k == KeyCombination.FromKey(Key.AltRight)) == e.AltPressed)
                && b.KeyCombination.Keys.All(k => k == InputKey.Control ? e.ControlPressed : k == InputKey.Shift ? e.ShiftPressed : k == InputKey.Alt ? e.AltPressed : down.Contains(k)))
				.OrderByDescending(b => b.KeyCombination.Keys.Count());
		}
        public bool OnPressed(KeyBindingPressEvent<ShredEditorAction> e)
        {
            if (GetContainingInputManager()?.FocusedDrawable is osu.Framework.Graphics.UserInterface.TextBox) return false;
            if (e.Action == ShredEditorAction.ToggleStepRecorder)
            {
                if (!e.Repeat) Enabled.Value = !Enabled.Value;
                return true;
            }
            if ((int)e.Action < 11 || (int)e.Action > 14) return false;
            if (!e.Repeat) ApplyGridCommand(e.Action);
            return true;
        }
        public void OnReleased(KeyBindingReleaseEvent<ShredEditorAction> e) { }

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
            var focused = GetContainingInputManager()?.FocusedDrawable;
            if (focused is osu.Framework.Graphics.UserInterface.TextBox) return false;
            IKeyBinding gridCommand = matching(editorBindings.Mappings, e)
                .FirstOrDefault(b => Convert.ToInt32(b.Action) >= 11 && Convert.ToInt32(b.Action) <= 14);
            if (gridCommand != null)
            {
                if (!e.Repeat)
                {
                    ApplyGridCommand((ShredEditorAction)Convert.ToInt32(gridCommand.Action));
                }
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
            // Tool keys belong to the mouse editor even while recording, including
            // when a custom gameplay binding also uses one of these number keys.
            if (IsMouseToolKey(e.Key) && !e.ControlPressed && !e.AltPressed && !e.SuperPressed && !e.ShiftPressed)
            {
                if (!e.Repeat) finishGesture();
                return false;
            }
			if (e.Key == Key.Space && !e.AltPressed && !e.SuperPressed && !e.ShiftPressed
                && (!e.ControlPressed || composer.Playback.ReturnOnPause.Value))
			{
				if (!e.Repeat)
				{
					finishGesture();
					composer.Playback.Toggle(composer.Playback.ReturnOnPause.Value && !e.ControlPressed, e.ControlPressed);
				}
				return true;
			}
			if ((e.Key == Key.Left || e.Key == Key.Right) && !e.ControlPressed && !e.AltPressed && !e.SuperPressed && !e.ShiftPressed)
                composer.Playback.NotifyNavigation();
            if (!Enabled.Value || !HasFocus) return false;
            if (e.Key == Key.Escape)
            {
                finishGesture();
                if (beatmap.SelectedHitObjects.Count == 0) return false;
                beatmap.SelectedHitObjects.Clear();
                return true;
            }
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
					startAtCursor(false);
				}
				return true;
			}
			bool isEmptyStep = matching(editorBindings.Mappings, e).Any(binding =>
				Convert.ToInt32(binding.Action) == (int)ShredEditorAction.EmptyStep);
			if ((e.Key == Key.Left || e.Key == Key.Right) && !e.ControlPressed && !e.AltPressed && !e.SuperPressed && !e.ShiftPressed
				&& (!isEmptyStep || editorClock.IsRunning || recorder?.HasHeldInputs != true))
			{
                composer.Playback.NotifyNavigation();
                // While playing, use the native editor's larger, smooth seeks.
                // Tiny subdivision seeks were overtaken by playback between repeats.
                if (editorClock.IsRunning) { finishGesture(); return false; }
                double position = editorClock.CurrentTimeAccurate;
                double target = nextStep(position);
                if (e.Key == Key.Left)
                {
                    target = composer.Grid.PreviousStep(position);
                }
                if (!editorClock.IsRunning)
                {
                    if (recorder == null) startAtCursor();
                    recorder.Navigate(Math.Max(0, target));
                    composer.Playback.NotifyNavigation(target);
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
			if (activeTool != composer.RecorderArticulation) finishGesture();
            if (recorder == null || (!recorder.HasHeldInputs && Math.Abs(editorClock.CurrentTimeAccurate - recorder.Position) > 0.5))
            {
                finishGesture();
                startAtCursor();
            }
			IKeyBinding editor = matching(editorBindings.Mappings, e).FirstOrDefault(b => Convert.ToInt32(b.Action) >= 2);
			if (editor != null)
			{
                if ((ShredEditorAction)Convert.ToInt32(editor.Action) == ShredEditorAction.EmptyStep)
                {
                    // With no held frets this is ordinary, repeatable forward navigation.
                    if (!recorder.HasHeldInputs) recorder.Navigate(nextStep(editorClock.CurrentTimeAccurate));
                    else if (!e.Repeat) recorder.EmptyStep();
                    composer.Playback.NotifyNavigation(recorder.Position);
                    return true;
                }
                if (e.Repeat || !consumedKeys.Add(e.Key)) return true;
				switch ((ShredEditorAction)Convert.ToInt32(editor.Action))
				{
					case ShredEditorAction.CycleGridSpacing:
						divisor.SelectNext();
						break;
					case ShredEditorAction.CycleGridType: cycleMode(1); break;
				}
				return true;
			}
			ShredAction[] actions = matching(gameplayBindings.Mappings, e)
				.Select(b => (ShredAction)Convert.ToInt32(b.Action))
				.Where(a => a == ShredAction.Wham || ShredBindingProfile.TryDecode(a, out _, out _)).Distinct().ToArray();
			if (actions.Length > 0)
			{
                if (e.Repeat || !consumedKeys.Add(e.Key)) return true;
				// Consume recording keys even when the current tool has no articulation.
				if (!composer.RecorderArticulation.HasValue) return true;
				actions = actions.Where(a => a == ShredAction.Wham
					|| (ShredBindingProfile.TryDecode(a, out _, out int index) && index < 5)).ToArray();
				physicalKeys[e.Key] = actions;
				foreach (ShredAction action in actions)
				{
					actionCounts.TryGetValue(action, out int count);
					actionCounts[action] = count + 1;
					if (count == 0) press(action);
				}
				return true;
			}
			// Unassigned keys retain their normal editor action.
			return false;
		}
		protected override void OnKeyUp(KeyUpEvent e)
		{
			consumedKeys.Remove(e.Key);
			if (!physicalKeys.Remove(e.Key, out ShredAction[] actions)) return;
			foreach (ShredAction action in actions)
			{
				if (--actionCounts[action] > 0) continue;
				actionCounts.Remove(action);
				release(action);
			}
		}
		public void ApplyGridCommand(ShredEditorAction action)
        {
            switch (action)
            {
                case ShredEditorAction.IncreaseGridSpacing: divisor.SelectNext(); break;
                case ShredEditorAction.DecreaseGridSpacing: divisor.SelectPrevious(); break;
                case ShredEditorAction.DecreaseGridType: cycleMode(-1); break;
                case ShredEditorAction.IncreaseGridType: cycleMode(1); break;
            }
        }
        private void cycleMode(int direction)
		{
			BeatDivisorPresetCollection current = divisor.ValidDivisors.Value;
			if (current.Type == BeatDivisorType.Custom) customDivisors = current;
			var modes = new List<BeatDivisorPresetCollection> { BeatDivisorPresetCollection.COMMON, BeatDivisorPresetCollection.TRIPLETS };
			if (customDivisors != null) modes.Add(customDivisors);
			int index = modes.FindIndex(mode => mode.Type == current.Type);
			divisor.ValidDivisors.Value = modes[(index + direction + modes.Count) % modes.Count];
		}
		protected override void OnFocusLost(FocusLostEvent e)
		{
			finishGesture();
			base.OnFocusLost(e);
		}
        protected override bool OnScroll(ScrollEvent e)
        {
            if (!e.ControlPressed && !e.AltPressed && !e.SuperPressed && !e.ShiftPressed)
                composer.Playback.NotifyNavigation();
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
			if (activeTool != composer.RecorderArticulation) finishGesture();
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
			if (isDisposing) { finishGesture(); gridMemory?.Dispose(); preferences?.Dispose(); }
			base.Dispose(isDisposing);
		}

		internal void FinishGridMemory()
		{
			gridMemory?.Dispose();
			// A new editor can open before the outgoing screen is disposed.
			// Flush its settings now instead of waiting for the delayed save.
			if (preferences != null) ((osu.Framework.Configuration.IConfigManager)preferences).Save();
		}
	}
}
