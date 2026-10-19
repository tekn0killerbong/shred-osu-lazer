using System.Collections.Generic;
using System.Numerics;

namespace osu.Game.Rulesets.Shred.Input
{
	/// <summary>Only successfully established, continuously held regular shapes
	/// can alternate without releasing their frets. Pick gestures retain their roles.</summary>
	public sealed class ShredStrumHistory
	{
		private readonly HashSet<int>[] shapes = { new HashSet<int>(), new HashSet<int>() };

		public void Remember(int row, int frets) => shapes[row].Add(frets);

		public void ForgetFret(int row, int fret)
		{
			shapes[row].RemoveWhere(mask => (mask & fret) != 0);
		}

		public void ResetRow(int row) => shapes[row].Clear();
		public void Reset() { ResetRow(0); ResetRow(1); }

		public bool CanAlternate(int row, int target, int other, int held)
		{
			if (target == 0 || other == 0 || (target & other) != 0) return false;
			if (!shapes[row].Contains(target) || !shapes[row].Contains(other)) return false;
			if ((held & (target | other)) != (target | other)) return false;
			// Ordinary single-note fret priority remains unchanged.
			return BitOperations.PopCount((uint)(target | other)) >= 3;
		}

		public int Capacity(int target, int other, int picks, int otherPicks, int availablePicks = 4)
		{
			int targetSize = BitOperations.PopCount((uint)target);
			int otherSize = BitOperations.PopCount((uint)other);
			// Five frets share four picks. Discount exactly one pick, and only
			// when the other gesture pays its full size: never discount both sides.
			if (targetSize + otherSize == availablePicks + 1 && targetSize > 1
				&& picks >= targetSize - 1 && otherPicks >= otherSize)
				return System.Math.Max(picks, targetSize);
			return picks;
		}
	}
}
