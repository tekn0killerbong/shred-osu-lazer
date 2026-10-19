using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Shred.Compatibility
{
    // Selection gestures share the native undo timeline, including menu Undo/Redo.
    // Their beatmap bytes remain identical, so selection alone never dirties a map.
    public sealed class ShredEditorHistory : IDisposable
    {
        private readonly EditorBeatmap beatmap;
        private readonly EditorChangeHandler history;
        private readonly List<Snapshot> snapshots = new List<Snapshot>();
        private int current;
        private bool applyingSelection;
        private bool selectionStateOpen;
        private bool appendingSelection;
        private bool absorbSelection;
        private byte[][] abandonedRedo;
        private List<Snapshot> abandonedSnapshots;
        private static readonly FieldInfo statesField = typeof(EditorChangeHandler).GetField("savedStates", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo indexField = typeof(EditorChangeHandler).GetField("currentState", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo changedField = typeof(EditorChangeHandler).GetField("OnStateChange", BindingFlags.Instance | BindingFlags.NonPublic);
        // The preview has its own optional selection list; official lazer does not.
        private static readonly FieldInfo nativeSelectionsField = typeof(EditorChangeHandler).GetField("savedSelections", BindingFlags.Instance | BindingFlags.NonPublic);
        private int[][] abandonedNativeSelections;
        private byte[] trimmedState;
        private Snapshot trimmedSnapshot;
        private int[] trimmedNativeSelection;
        private readonly Dictionary<HitObject, int> indices = new();
        private static readonly System.Reflection.FieldInfo restoringField = typeof(EditorChangeHandler).GetField("isRestoring",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private sealed class Snapshot
        {
            public string Hash;
            public object Token;
            public HashSet<int> Selection = new();
        }
        public ShredEditorHistory(EditorBeatmap beatmap, EditorChangeHandler history)
        {
            this.beatmap = beatmap;
            this.history = history;
#if !SHRED_OFFICIAL
            if (history is BeatmapEditorChangeHandler preview) preview.SelectionHistoryHandledExternally = true;
#endif
            snapshots.Add(snapshot(history.CurrentStateHash));
            rebuildIndices();
            captureSelection();
            beatmap.TransactionBegan += beginEdit;
            history.TransactionBegan += beginEdit;
            beatmap.SelectedHitObjects.CollectionChanged += selectionChanged;
            history.OnStateChange += stateChanged;
        }
        private object stateToken()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(EditorChangeHandler);
            var states = type.GetField("savedStates", flags)?.GetValue(history) as System.Collections.IList;
            var index = type.GetField("currentState", flags)?.GetValue(history) as int?;
            // A future lazer API change safely disables identity matching on a normal edit.
            return states != null && index >= 0 && index < states.Count ? states[index.Value] : new object();
        }
        private Snapshot snapshot(string hash) => new Snapshot
        {
            Hash = hash,
            Token = stateToken()
        };
        private bool restoring => (bool?)restoringField?.GetValue(history) == true;
        internal bool CanProcessInsertions => restoringField != null && !restoring;
        private void rebuildIndices()
        {
            indices.Clear();
            for (int i = 0; i < beatmap.HitObjects.Count; i++) indices[beatmap.HitObjects[i]] = i;
        }
        private void selectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (beatmap.TransactionActive || history.TransactionActive || restoring || applyingSelection) return;
            if (!selectionStateOpen && !absorbSelection)
            {
                // The old snapshot still contains the selection before this event.
                if (appendSelectionState()) return;
            }
            // A rectangle selection raises one event per note. Update only those
            // indices instead of rescanning the whole map for every selected note.
            var selected = snapshots[current].Selection;
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) selected.Clear();
            if (e.OldItems != null)
                foreach (HitObject note in e.OldItems)
                    if (indices.TryGetValue(note, out int index)) selected.Remove(index);
            if (e.NewItems != null)
                foreach (HitObject note in e.NewItems)
                    if (indices.TryGetValue(note, out int index)) selected.Add(index);
        }
        private void beginEdit()
        {
            if (restoring || applyingSelection) return;
            FinishFrame();
            captureSelection();
        }

        public void FinishFrame(bool mouseHeld = false)
        {
            absorbSelection = false;
            if (mouseHeld || !selectionStateOpen) return;
            selectionStateOpen = false;
            if (current > 0 && snapshots[current].Hash == snapshots[current - 1].Hash
                && snapshots[current].Selection.SetEquals(snapshots[current - 1].Selection)
                && statesField?.GetValue(history) is List<byte[]> states
                && indexField?.GetValue(history) is int index && index == states.Count - 1)
            {
                // Clicking an already selected note or cancelling a drag may end
                // exactly where it started. Keep the old redo branch in that case.
                states.RemoveAt(index);
                if (abandonedRedo != null) states.AddRange(abandonedRedo);
                if (nativeSelectionsField?.GetValue(history) is List<int[]> nativeSelections)
                {
                    nativeSelections.RemoveAt(index);
                    if (abandonedNativeSelections != null) nativeSelections.AddRange(abandonedNativeSelections);
                }
                snapshots.RemoveAt(current--);
                if (trimmedState != null)
                {
                    states.Insert(0, trimmedState);
                    snapshots.Insert(0, trimmedSnapshot);
                    if (nativeSelectionsField?.GetValue(history) is List<int[]> saved) saved.Insert(0, trimmedNativeSelection);
                    current++;
                    index++;
                }
                indexField.SetValue(history, index - 1);
                if (abandonedSnapshots != null) snapshots.AddRange(abandonedSnapshots);
                updateAvailability(states.Count, index - 1);
                (changedField.GetValue(history) as Action)?.Invoke();
            }
            abandonedRedo = null;
            abandonedSnapshots = null;
            abandonedNativeSelections = null;
            trimmedState = null;
            trimmedSnapshot = null;
            trimmedNativeSelection = null;
        }

        private bool appendSelectionState()
        {
            if (statesField?.GetValue(history) is not List<byte[]> states
                || indexField?.GetValue(history) is not int index || index < 0 || index >= states.Count
                || changedField == null) return false;
            abandonedRedo = states.Skip(index + 1).ToArray();
            abandonedSnapshots = snapshots.Skip(current + 1).ToList();
            if (index + 1 < states.Count) states.RemoveRange(index + 1, states.Count - index - 1);
            if (nativeSelectionsField?.GetValue(history) is List<int[]> nativeSelections)
            {
                abandonedNativeSelections = nativeSelections.Skip(index + 1).ToArray();
                if (index + 1 < nativeSelections.Count) nativeSelections.RemoveRange(index + 1, nativeSelections.Count - index - 1);
                nativeSelections.Add(Array.Empty<int>());
            }
            // Separate identity, identical bytes: native undo can traverse this
            // step and CurrentStateHash stays unchanged for save/dirty detection.
            states.Add((byte[])states[index].Clone());
            if (states.Count > EditorChangeHandler.MAX_SAVED_STATES + 1)
            {
                trimmedState = states[0];
                trimmedSnapshot = snapshots[0];
                states.RemoveAt(0);
                if (nativeSelectionsField?.GetValue(history) is List<int[]> saved)
                {
                    trimmedNativeSelection = saved[0];
                    saved.RemoveAt(0);
                }
            }
            indexField.SetValue(history, states.Count - 1);
            appendingSelection = true;
            try { (changedField.GetValue(history) as Action)?.Invoke(); }
            finally { appendingSelection = false; }
            selectionStateOpen = true;
            updateAvailability(states.Count, states.Count - 1);
            return true;
        }

        private void updateAvailability(int count, int index)
        {
            history.CanUndo.Value = index > 0;
            history.CanRedo.Value = index < count - 1;
        }

        private void captureSelection()
        {
            // The patcher's undo/redo transaction must never overwrite snapshots
            // with its intermediate removals of moved or resized objects.
            if (restoring || applyingSelection) return;
            snapshots[current].Selection = beatmap.SelectedHitObjects
                .Where(indices.ContainsKey).Select(n => indices[n]).ToHashSet();
        }
        private void stateChanged()
        {
            string hash = history.CurrentStateHash;
            object token = stateToken();
            int target = snapshots.FindIndex(s => ReferenceEquals(s.Token, token));
            if (target == current) return;
            if (target >= 0)
            {
                selectionStateOpen = false;
                absorbSelection = false;
                abandonedRedo = null;
                abandonedSnapshots = null;
                abandonedNativeSelections = null;
                trimmedState = null;
                trimmedSnapshot = null;
                trimmedNativeSelection = null;
                var restored = snapshots[target];
                rebuildIndices();
                applyingSelection = true;
                try
                {
                    beatmap.SelectedHitObjects.Clear();
                    beatmap.SelectedHitObjects.AddRange(restored.Selection.Where(i => i >= 0 && i < beatmap.HitObjects.Count).Select(i => beatmap.HitObjects[i]));
                }
                finally { applyingSelection = false; }
                // History restores content and selection, never the editor's viewport.
                current = target;
                return;
            }
            if (current + 1 < snapshots.Count) snapshots.RemoveRange(current + 1, snapshots.Count - current - 1);
            selectionStateOpen = false;
            absorbSelection = !appendingSelection;
            snapshots.Add(snapshot(hash));
            // Native history retains the current state plus MAX_SAVED_STATES predecessors.
            if (snapshots.Count > EditorChangeHandler.MAX_SAVED_STATES + 1) snapshots.RemoveAt(0);
            current = snapshots.Count - 1;
            rebuildIndices();
            captureSelection();
        }
        public void Dispose()
        {
            beatmap.TransactionBegan -= beginEdit;
            history.TransactionBegan -= beginEdit;
            beatmap.SelectedHitObjects.CollectionChanged -= selectionChanged;
            history.OnStateChange -= stateChanged;
        }
    }

}
