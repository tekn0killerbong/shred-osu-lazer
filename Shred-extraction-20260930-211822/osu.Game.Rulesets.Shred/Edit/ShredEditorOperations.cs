using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.UI;

namespace osu.Game.Rulesets.Shred.Edit
{
	public static class ShredEditorOperations
	{
		public static double DragTime(double originalTime, double mouseStartTime, double mouseEndTime, Func<double, double> snap)
        {
            return originalTime + snap(mouseEndTime) - snap(mouseStartTime);
        }

        public static double VisibleTime(double zoom)
		{
			if (!double.IsFinite(zoom) || zoom <= 0) zoom = 1;
			return ShredScrollSpeed.DefaultApproachTime / zoom;
		}

		// Selecting a blueprint changes draw order. Never enumerate the live collection while selecting.
		public static void SelectRange<T>(IEnumerable<T> source, Func<T, double> startTime, double from, double to, Action<T> select)
		{
			T[] range = source.Where(item => startTime(item) >= from && startTime(item) <= to).ToArray();
			foreach (T item in range) select(item);
		}

		public static bool Flip(IEnumerable<ShredHitObject> source, bool horizontal, bool overOrigin, Action<ShredHitObject> update)
		{
			ShredHitObject[] notes = source.Where(n => !n.IsWham).Distinct().ToArray();
			if (horizontal) notes = notes.Where(n => n.Fret > 0).ToArray();
			if (notes.Length == 0) return false;
			int sum;
			if (horizontal)
			{
				sum = 6;
				if (!overOrigin) sum = notes.Min(n => n.Fret) + notes.Max(n => n.Fret);
			}
			else
			{
				sum = 1;
				if (!overOrigin) sum = notes.Min(n => n.Row) + notes.Max(n => n.Row);
			}
			foreach (ShredHitObject note in notes)
			{
				if (horizontal) note.Fret = sum - note.Fret;
				else note.Row = sum - note.Row;
				update(note);
			}
			return true;
		}

		public static void InvertTypes(IEnumerable<ShredHitObject> source, Action<ShredHitObject> update)
		{
			foreach (ShredHitObject note in source.Distinct().ToArray())
			{
				if (note.IsWham || note.Fret == 0) continue;
				if (note.Articulation == ShredPracticeArticulation.Pick) note.Articulation = ShredPracticeArticulation.HammerPull;
				else note.Articulation = ShredPracticeArticulation.Pick;
				update(note);
			}
		}

		public static bool Conflicts(ShredHitObject a, ShredHitObject b)
		{
			if (a.IsWham != b.IsWham || (!a.IsWham && a.Column != b.Column)) return false;
			if (Math.Abs(a.StartTime - b.StartTime) < 0.5) return true;
			return a.StartTime < b.EndTime - 0.5 && b.StartTime < a.EndTime - 0.5;
		}

		public static void ConvertType(IEnumerable<ShredHitObject> source, ShredPracticeArticulation type, Action<ShredHitObject> update)
		{
			foreach (ShredHitObject note in source.Distinct().ToArray())
			{
				// OPEN has no fret to hammer/pull; whams keep their own articulation.
				if (note.IsWham || (note.Fret == 0 && type == ShredPracticeArticulation.HammerPull)) continue;
				note.Articulation = type;
				update(note);
			}
		}
	}
}
