namespace osu.Game.Rulesets.Shred.Gameplay
{
	/// <summary>Fret 1 is leftmost and highest; fret 5 is rightmost and lowest.</summary>
	public static class ShredChordRules
	{
		public static int MaskFor(int fret) => fret == 0 ? 0 : 1 << (fret - 1);

		public static bool IsExact(int held, int required, int sustaining)
		{
			int wanted = required | sustaining;
			if (wanted == 0) return held == 0;
			int rightmost = 0;
			for (int i = 0; i < 5; i++) if ((wanted & (1 << i)) != 0) rightmost = i;
			int lower = 31 & ~((1 << (rightmost + 1)) - 1);
			return (held & wanted) == wanted && (held & ~(wanted | lower)) == 0;
		}

		public static bool IsHammerPull(int before, int after, int required, bool pressed, int sustaining = 0)
		{
			if (before == after || required == 0 || !IsExact(after, required, sustaining)) return false;
			if (pressed) return ((after & ~before) & required) != 0;
			// Removing a wrong higher (left-hand) fret reveals the requested note/chord.
			return !IsExact(before, required, sustaining);
		}
	}
}
