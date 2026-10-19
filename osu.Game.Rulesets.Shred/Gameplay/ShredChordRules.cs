namespace osu.Game.Rulesets.Shred.Gameplay
{
    public static class ShredChordRules
    {
        // Legacy exports can floor one note while an editor-created neighbour
        // keeps its fractional timestamp. Sub-millisecond differences still
        // represent one chord; retain the actual timestamps for judgement.
        public const double ChordTolerance = 1;
        public static bool SameChordTime(double a, double b) => System.Math.Abs(a - b) < ChordTolerance;

        public static int MaskFor(int fret) => fret == 0 ? 0 : 1 << (fret - 1);
        public static int PickCount(int required) => System.Math.Max(1, System.Numerics.BitOperations.PopCount((uint)required));

        // Strictly between the endpoints; a single note has no interior.
        public static int InteriorMask(int chord)
        {
            if (System.Numerics.BitOperations.PopCount((uint)chord) < 2) return 0;
            int left = System.Numerics.BitOperations.TrailingZeroCount((uint)chord);
            int right = 31 - System.Numerics.BitOperations.LeadingZeroCount((uint)chord);
            return ((1 << right) - 1) & ~((1 << (left + 1)) - 1);
        }

        // Sustains are exempt: a new attack must remain possible while another LN is held.
        // Extras may lie behind the requested chord, never inside it or ahead of it.
        public static bool IsExact(int held, int required, int sustaining, int direction = -1)
        {
            if ((held & required) != required) return false;
            int extras = held & ~sustaining & ~required;
            if (required == 0) return (held & ~sustaining) == 0;
            int left = 0, right = 4;
            while ((required & (1 << left)) == 0) left++;
            while ((required & (1 << right)) == 0) right--;
            int allowed = direction < 0 ? 31 & ~((1 << (right + 1)) - 1) : (1 << left) - 1;
            return (extras & ~allowed) == 0;
        }

        public static bool IsHammerPull(int before, int after, int required, bool pressed, int sustaining = 0, int direction = -1)
        {
            if (before == after || !IsExact(after, required, sustaining, direction)) return false;
            if (required == 0) return !pressed && before != 0 && after == 0;
            if (pressed) return ((after & ~before) & required) != 0;
            return !IsExact(before, required, sustaining, direction);
        }
    }

    public sealed class ShredFretSequence
    {
        public int Direction => DirectionFor(0);
        private readonly int[] rowDirection = { -1, -1 };
        public int DirectionFor(int row) => rowDirection[row];
        private long nextOrder;
        private readonly long[,] rowPressOrder = new long[2, 5];

        public void Press(int fret, int heldBefore, int row = 0)
        {
            // Compare against the most recent fret STILL held, never a released key.
            int reference = -1;
            for (int i = 0; i < 5; i++)
                if ((heldBefore & (1 << i)) != 0 && (reference < 0 || rowPressOrder[row, i] > rowPressOrder[row, reference]))
                    reference = i;
            if (reference >= 0 && reference != fret) rowDirection[row] = fret > reference ? 1 : -1;
            rowPressOrder[row, fret] = ++nextOrder;
        }

        // New presses outrank older holds on either side, even when reversing
        // between two held frets. Chords still prohibit extra frets inside them.
        public bool IsExact(int held, int required, int sustaining, int row = 0)
        {
            if ((held & required) != required) return false;
            if (required == 0) return (held & ~sustaining) == 0;
            int extras = held & ~required & ~sustaining;
            if (extras == 0) return true;
            int left = 0, right = 4;
            while ((required & (1 << left)) == 0) left++;
            while ((required & (1 << right)) == 0) right--;
            int span = ((1 << (right + 1)) - 1) & ~((1 << left) - 1);
            if ((extras & span) != 0) return false;
            long oldestRequired = long.MaxValue;
            for (int i = 0; i < 5; i++)
                if ((required & (1 << i)) != 0)
                    oldestRequired = System.Math.Min(oldestRequired, rowPressOrder[row, i]);
            for (int i = 0; i < 5; i++)
                if ((extras & (1 << i)) != 0 && rowPressOrder[row, i] >= oldestRequired) return false;
            return true;
        }

        public bool IsHammerPull(int before, int after, int required, bool pressed, int sustaining, int row = 0)
        {
            if (before == after || !IsExact(after, required, sustaining, row)) return false;
            if (required == 0) return !pressed && before != 0 && after == 0;
            if (pressed) return ((after & ~before) & required) != 0;
            return !IsExact(before, required, sustaining, row);
        }
        public long PressOrder(int fret, int row = 0) => fret == 0 ? long.MinValue : rowPressOrder[row, fret - 1];

        public void Reset() { System.Array.Fill(rowDirection, -1); System.Array.Clear(rowPressOrder, 0, rowPressOrder.Length); nextOrder = 0; }
    }
}
