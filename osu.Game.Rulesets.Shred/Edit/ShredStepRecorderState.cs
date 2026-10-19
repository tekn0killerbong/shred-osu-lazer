using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Shred.Edit
{
    /// <summary>Empty steps extend sustains; only short-note gestures advance on final release.</summary>
    public sealed class ShredStepRecorderState
    {
        public sealed class Note
        {
            public int Column { get; }
            public double Start { get; }
            public double End { get; internal set; }
            public bool Sustain { get; internal set; }
            public bool Picked { get; internal set; }
            internal Note(int column, double start) { Column = column; Start = End = start; }
        }

        private readonly Dictionary<int, Note> held = new();
        private readonly HashSet<int> pickModifiers = new();
        private readonly List<Note> currentChord = new();
		private bool gestureHasSustain;
        private readonly Func<double, double> nextStep;
        private readonly Action<Note> add;
        private readonly Action<Note> update;
        private readonly Action<double> seek;
        public double Position { get; private set; }
        public bool HasHeldInputs => held.Count > 0 || pickModifiers.Count > 0;
        public bool IsHeld(int column) => held.ContainsKey(column);

        public ShredStepRecorderState(double position, Func<double, double> nextStep,
            Action<Note> add, Action<Note> update, Action<double> seek)
        {
            Position = position;
            this.nextStep = nextStep;
            this.add = add;
            this.update = update;
            this.seek = seek;
        }

        public void Press(int column, bool picked = false)
        {
            if (held.ContainsKey(column)) return;
            Note note = new Note(column, Position) { Picked = picked || column == 12 || column % 6 == 0 };
            held.Add(column, note);
            currentChord.RemoveAll(n => n.Column == column && Math.Abs(n.Start - Position) < 0.000001);
            currentChord.Add(note);
            add(note);
        }

        public void Pick(IEnumerable<int> columns)
        {
            foreach (int column in columns.Distinct())
            {
                if (!held.ContainsKey(column) && (column == 12 || column % 6 == 0)) Press(column, true);
                // Released members still belong to the chord until its last key is released.
                foreach (Note note in currentChord.Concat(held.Values).Distinct().Where(n => n.Column == column && !n.Picked))
                {
                    note.Picked = true;
                    update(note);
                }
            }
        }

        public void Release(int column)
        {
            if (!held.Remove(column)) return;
			finishRelease();
        }

        public void HoldPickModifier(int action) => pickModifiers.Add(action);

        public void ReleasePickModifier(int action)
        {
			if (pickModifiers.Remove(action)) finishRelease();
        }

        public void EmptyStep()
        {
			foreach (Note note in held.Values)
			{
				if (note.Column != 12 && note.Column % 6 == 0) continue;
				note.Sustain = true;
				gestureHasSustain = true;
			}
            advance();
        }

        public void Navigate(double target)
        {
            if (!double.IsFinite(target)) return;
            target = Math.Max(0, target);
            foreach (Note note in held.Values.Where(n => n.Sustain))
            {
                note.End = Math.Max(note.Start, target);
                update(note);
            }
            Position = target;
            currentChord.Clear();
            seek(Position);
        }

        public void Finish()
        {
            held.Clear();
            pickModifiers.Clear();
            currentChord.Clear();
			gestureHasSustain = false;
        }

		private void finishRelease()
		{
			if (HasHeldInputs) return;
			// Empty Step already moved to the LN end. Keep this decision for the
			// entire gesture so the order of chord releases cannot add a step.
			if (!gestureHasSustain) advance();
			gestureHasSustain = false;
			currentChord.Clear();
		}

        private void advance()
        {
            double next = nextStep(Position);
            if (!double.IsFinite(next) || next <= Position + 0.000001) return;
            foreach (Note note in held.Values.Where(n => n.Sustain))
            {
                note.End = Math.Max(note.Start, next);
                update(note);
            }
            Position = next;
            currentChord.Clear();
            seek(Position);
        }
    }
}
