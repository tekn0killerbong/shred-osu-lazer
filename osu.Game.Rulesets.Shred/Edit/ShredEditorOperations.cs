using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;

namespace osu.Game.Rulesets.Shred.Edit
{
	public static class ShredEditorOperations
	{
		// Preserve the editor's 100% reference independently of gameplay scroll settings.
		private const double base_visible_time = 11485.0 / 20;

		public static int PasteRow(int row, int copiedRow, int targetRow) => copiedRow == targetRow ? row : 1 - row;

        public static double DragDuration(double originalStart, double originalEnd) => Math.Max(0, originalEnd - originalStart);

        public static double DragTime(double originalTime, double mouseStartTime, double mouseEndTime, Func<double, double> snap)
        {
            return originalTime + DragDelta(originalTime, mouseStartTime, mouseEndTime, snap);
        }

        public static double DragDelta(double anchor, double mouseStartTime, double mouseEndTime, Func<double, double> snap)
            => snap(anchor + mouseEndTime - mouseStartTime) - snap(anchor);

        public static double VisibleTime(double zoom)
		{
			if (!double.IsFinite(zoom) || zoom <= 0) zoom = 1;
			return base_visible_time / zoom;
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
				if (note.IsWham) continue;
				if (note.Articulation == ShredPracticeArticulation.Pick) note.Articulation = ShredPracticeArticulation.HammerPull;
				else note.Articulation = ShredPracticeArticulation.Pick;
				update(note);
			}
		}

		public static void ResolveTailConflicts(IEnumerable<ShredHitObject> source, Func<double, double> previousStep, Action<ShredHitObject> update, IEnumerable<ShredHitObject> changed = null)
        {
            var notes = source.ToArray();
            var affected = changed?.Where(n => n != null).ToHashSet();
            foreach (var ln in notes.Where(n => n.IsLong))
            {
                var next = notes.Where(n => n != ln && n.IsWham == ln.IsWham
                    && (ln.IsWham || n.Column == ln.Column)
                    && n.StartTime > ln.StartTime && !ShredChordRules.SameChordTime(n.StartTime, ln.StartTime)
                    && n.StartTime < ln.EndTime && !ShredChordRules.SameChordTime(n.StartTime, ln.EndTime)
                    && (affected == null || affected.Contains(ln) || affected.Contains(n)))
                    .OrderBy(n => n.StartTime).FirstOrDefault();
                if (next == null) continue;
                ln.Duration = next.StartTime - ln.StartTime;
				ln.TailSnapDivisor = next.HeadSnapDivisor;
                update(ln);
            }
        }

        public static bool SameHead(ShredHitObject a, ShredHitObject b)
            => a.IsWham == b.IsWham && (a.IsWham || a.Column == b.Column)
                && ShredChordRules.SameChordTime(a.StartTime, b.StartTime);

        public static bool Conflicts(ShredHitObject a, ShredHitObject b)
		{
			if (a.IsWham != b.IsWham || (!a.IsWham && a.Column != b.Column)) return false;
			if (ShredChordRules.SameChordTime(a.StartTime, b.StartTime)) return true;
			return a.StartTime < b.EndTime - 0.5 && b.StartTime < a.EndTime - 0.5;
		}

		public static void ConvertType(IEnumerable<ShredHitObject> source, ShredPracticeArticulation type, Action<ShredHitObject> update)
		{
			foreach (ShredHitObject note in source.Distinct().ToArray())
			{
				// OPEN can be played by a pull-off; whams retain their articulation.
				if (note.IsWham) continue;
				note.Articulation = type;
				update(note);
			}
		}
	}
}
