using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Shred.Input;

namespace osu.Game.Rulesets.Shred.Gameplay
{
	public enum ShredPracticeArticulation
	{
		Pick,
		HammerOn,
		PullOff,
		HammerPull,
		Wham,
	}

	public enum ShredPracticeResult
	{
		Pending,
		Hit,
		Miss,
	}

	public sealed class ShredPracticeNote
	{
		public double Time { get; }
		public int Row { get; }
		public int Fret { get; }
		public ShredPracticeArticulation Articulation { get; }
		public ShredPracticeResult Result { get; internal set; }
		public double Offset { get; internal set; }

		public ShredPracticeNote(double time, int row, int fret,
			ShredPracticeArticulation articulation = ShredPracticeArticulation.Pick)
		{
			if (!double.IsFinite(time) || time < 0)
				throw new ArgumentOutOfRangeException(nameof(time));
			if (row < 0 || row >= 2)
				throw new ArgumentOutOfRangeException(nameof(row));
			if (fret < 0 || fret > 5)
				throw new ArgumentOutOfRangeException(nameof(fret));
			Time = time;
			Row = row;
			Fret = fret;
			Articulation = articulation;
		}
	}

	// Temporary practice engine, independent of rendering and keyboard layout.
	// The 120 ms diagnostic window is NOT the final mania hit-window/scoring system.
	public sealed class ShredPracticeSession
	{
		public const int RowCount = 2;
		public const double HitWindow = 120;
		private readonly ShredStringState[] strings;
		private readonly ShredPracticeNote[] notes;

		public IReadOnlyList<ShredPracticeNote> Notes => notes;
		public double Time { get; private set; }
		public int Hits { get; private set; }
		public int Misses { get; private set; }
		public int Combo { get; private set; }
		public string LastEvent { get; private set; } = "Ready";
		public bool Completed => notes.All(note => note.Result != ShredPracticeResult.Pending);

		public ShredPracticeSession(IEnumerable<ShredPracticeNote> source)
		{
			strings = new ShredStringState[RowCount];
			for (int row = 0; row < RowCount; row++)
				strings[row] = new ShredStringState();

			// Copy definitions so two sessions cannot mutate each other's results.
			notes = source.OrderBy(note => note.Time)
				.Select(note => new ShredPracticeNote(note.Time, note.Row, note.Fret, note.Articulation))
				.ToArray();
			if (notes.Any(note => note.Row >= RowCount))
				throw new ArgumentException("A note refers to an unavailable string.", nameof(source));
		}

		public ShredStringState GetString(int row) => strings[row];

		public void AdvanceTo(double time)
		{
			if (!double.IsFinite(time) || time < Time)
				throw new ArgumentOutOfRangeException(nameof(time));
			Time = time;
			foreach (ShredPracticeNote note in notes)
			{
				if (note.Result != ShredPracticeResult.Pending || Time <= note.Time + HitWindow)
					continue;
				note.Result = ShredPracticeResult.Miss;
				Misses++;
				Combo = 0;
				LastEvent = $"MISS - string {note.Row + 1}";
			}
		}

		public void SetFret(int row, int fretIndex, bool pressed, bool judge = true)
		{
			int previous = strings[row].SelectedFret;
			strings[row].SetFret(fretIndex, pressed);
			int selected = strings[row].SelectedFret;
			if (!judge || previous == selected)
				return;

			ShredPracticeNote note = candidate(row);
			if (note == null || note.Fret != selected)
				return;
			// Pull-off notes accept the same upward tap as a hammer-on,
			// and additionally accept releasing a higher fret onto the target.
			bool hammer = pressed && (previous == 0 || selected < previous)
				&& note.Articulation != ShredPracticeArticulation.Pick;
			bool pull = !pressed && selected > previous
				&& note.Articulation != ShredPracticeArticulation.Pick;
			if (hammer || pull)
				hit(note);
		}

		public void SetPick(int row, int pickIndex, bool pressed, bool judge = true)
		{
			bool edge = strings[row].SetPick(pickIndex, pressed);
			if (!edge || !judge)
				return;
			ShredPracticeNote note = candidate(row);
			if (note != null && note.Fret == strings[row].SelectedFret)
				hit(note);
		}

		private ShredPracticeNote candidate(int row)
		{
			// Never skip an earlier unresolved note on the same string.
			foreach (ShredPracticeNote note in notes)
			{
				if (note.Row != row || note.Result != ShredPracticeResult.Pending)
					continue;
				if (Math.Abs(Time - note.Time) <= HitWindow)
					return note;
				return null;
			}
			return null;
		}

		private void hit(ShredPracticeNote note)
		{
			note.Result = ShredPracticeResult.Hit;
			note.Offset = Time - note.Time;
			Hits++;
			Combo++;
			LastEvent = $"HIT - string {note.Row + 1} / {note.Offset:+0;-0;0} ms";
		}
	}
}
