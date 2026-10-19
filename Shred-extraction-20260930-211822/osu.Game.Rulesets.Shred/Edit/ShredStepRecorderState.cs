using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Shred.Edit
{
	/// <summary>Real-time input, musical durations. The hold threshold never moves the cursor.</summary>
	public sealed class ShredStepRecorderState
	{
		public const double HoldDelay = 1000;
		public sealed class Note
		{
			public int Column { get; }
			public double Start { get; }
			public double End { get; internal set; }
			public bool Sustain { get; internal set; }
			public bool Picked { get; internal set; }
			internal bool Pending = true;
			internal double PickDeadline;
			internal Note(int column, double start) { Column = column; Start = End = start; }
		}

		private readonly Dictionary<int, Note> held = new Dictionary<int, Note>();
		private readonly List<Note> currentChord = new List<Note>();
		private readonly Func<double, double> nextStep;
		private readonly Action<Note> add;
		private readonly Action<Note> update;
		private readonly Action<double> seek;
		private double deadline = double.PositiveInfinity;
		public double Position { get; private set; }
		public bool HasHeldNotes => held.Count > 0;
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

		public void Press(int column, double now, bool picked = false)
		{
			if (held.ContainsKey(column)) return;
			Tick(now);
			Note note = new Note(column, Position) { Picked = picked || column == 12 || column % 6 == 0 };
			held.Add(column, note);
			currentChord.RemoveAll(n => n.Column == column && Math.Abs(n.Start - Position) < 0.000001);
			currentChord.Add(note);
			add(note);
			note.PickDeadline = now + HoldDelay;
			deadline = held.Values.Where(n => n.Pending).Min(n => n.PickDeadline);
		}

		public void Pick(IEnumerable<int> columns, double now)
		{
			Tick(now);
			foreach (int column in columns.Distinct())
			{
				if (!held.ContainsKey(column) && (column == 12 || column % 6 == 0))
					Press(column, now, true);
				// Also mark already released members of this still-open chord.
				foreach (Note note in currentChord.Concat(held.Values).Distinct().Where(n => n.Column == column && !n.Picked && now < n.PickDeadline))
				{
					note.Picked = true;
					update(note);
				}
			}
		}

		public void Release(int column, double now)
		{
			if (!held.TryGetValue(column, out Note note)) return;
			Tick(now);
			held.Remove(column);
			if (note.Pending && !held.Values.Any(n => n.Pending)) advance();
			if (!held.Values.Any(n => n.Pending)) deadline = double.PositiveInfinity;
		}

		public void Tick(double now)
		{
			if (now < deadline) return;
			deadline = double.PositiveInfinity;
			foreach (Note note in held.Values.Where(n => n.Pending && now >= n.PickDeadline))
			{
				note.Pending = false;
				if (note.Column == 12 || note.Column % 6 != 0) note.Sustain = true;
			}
			deadline = held.Values.Where(n => n.Pending).Select(n => n.PickDeadline).DefaultIfEmpty(double.PositiveInfinity).Min();
			// Arm the sustain, but do not invent musical length or move the grid.
			// An empty step or a subsequent short chord will extend held notes.
		}

		public void EmptyStep(double now)
		{
			foreach (Note note in held.Values)
			{
				if (note.Column == 12 || note.Column % 6 != 0) note.Sustain = true;
				note.Pending = false;
			}
			deadline = double.PositiveInfinity;
			advance();
		}

		public void Navigate(double target, double now)
		{
			if (!double.IsFinite(target)) return;
			Tick(now);
			target = Math.Max(0, target);
			foreach (Note note in held.Values)
			{
				if (note.Column == 12 || note.Column % 6 != 0)
				{
					note.Sustain = true;
					note.End = Math.Max(note.Start, target);
					update(note);
				}
				note.Pending = false;
			}
			deadline = double.PositiveInfinity;
			Position = target;
			currentChord.Clear();
			seek(Position);
		}

		public void Finish()
		{
			held.Clear();
			currentChord.Clear();
			deadline = double.PositiveInfinity;
		}

		private void advance()
		{
			double next = nextStep(Position);
			if (!double.IsFinite(next) || next <= Position + 0.000001) return;
			foreach (Note note in held.Values)
			{
				if (!note.Sustain) continue;
				note.End = next;
				update(note);
			}
			Position = next;
			currentChord.Clear();
			seek(Position);
		}
	}
}
