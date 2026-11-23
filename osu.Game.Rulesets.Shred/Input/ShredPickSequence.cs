using System;
using osu.Game.Rulesets.Shred.Gameplay;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Shred.Input
{
    /// <summary>
    /// Like fret priority, each new gesture outranks older held gestures on
    /// either side. Releasing it restores the most recent gesture still held.
    /// Only near-simultaneous presses aimed at the same chord form one gesture.
    /// </summary>
    public sealed class ShredPickSequence
    {
        public sealed class Gesture
        {
            public double? ChordTime;
            public int Mask;
            public int OriginalMask;
            private readonly int[] frets = new int[2];
            public int FretsFor(int row) => frets[row];
            public void RememberFrets(int row, int mask) => frets[row] = mask;
            public void ForgetFret(int row, int mask)
            {
                if ((frets[row] & mask) != 0) frets[row] = 0;
            }
            public double PressTime;
            public double JoinWindow;
            public bool CanJoin = true;
        }

        private readonly List<Gesture> gestures = new List<Gesture>();
        public Gesture Active
        {
            get
            {
                if (gestures.Count == 0) return null;
                return gestures[gestures.Count - 1];
            }
        }

        public Gesture PreviousFor(int row, bool pooled)
        {
            for (int i = gestures.Count - 2; i >= 0; i--)
            {
                int mask = gestures[i].Mask;
                if (!pooled) mask &= 15 << (row * 4);
                if (mask != 0 && gestures[i].FretsFor(row) != 0) return gestures[i];
            }
            return null;
        }

        public void ForgetFret(int row, int mask)
        {
            foreach (var gesture in gestures) gesture.ForgetFret(row, mask);
        }

        public int FretsForHeldPicks(int row, int picks)
        {
            int frets = 0;
            foreach (var gesture in gestures)
                if ((gesture.Mask & picks) != 0) frets |= gesture.FretsFor(row);
            return frets;
        }

        public void RememberFrets(int row, int mask, double chordTime, int physicalPicks)
        {
            // Releasing an old anchor to finish a four-note physical chord can
            // split its picks into separate gestures. Preserve that input
            // priority while remembering the shape they completed together.
            foreach (var gesture in gestures)
                if (gesture.ChordTime.HasValue && ShredChordRules.SameChordTime(gesture.ChordTime.Value, chordTime)
                    && (gesture.Mask & physicalPicks) != 0)
                    gesture.RememberFrets(row, mask);
        }

        public void Press(int row, int pick, double? chordTime, double? pressTime = null, double rate = 1)
        {
            int bit = 1 << (row * 4 + pick);
            // A repeated physical edge must not rearrange priority.
            if (gestures.Exists(g => (g.Mask & bit) != 0)) return;
            Gesture latest = gestures.Count == 0 ? null : gestures[gestures.Count - 1];
            double time = pressTime ?? chordTime ?? 0;
            double window = ShredPickBuffer<int>.WindowFor(double.PositiveInfinity, rate);
            // A late correction or reversal must become the active gesture,
            // even if the same unfinished chord is still the scoring target.
            // Measure from the FIRST press: a rolling series cannot keep an
            // old group open forever. Releasing that active group closes assembly too.
            if (latest == null || !chordTime.HasValue || !latest.ChordTime.HasValue
                || !ShredChordRules.SameChordTime(latest.ChordTime.Value, chordTime.Value)
                || !latest.CanJoin || time < latest.PressTime
                || time - latest.PressTime > Math.Min(window, latest.JoinWindow))
            {
                latest = new Gesture { ChordTime = chordTime, PressTime = time, JoinWindow = window };
                gestures.Add(latest);
            }
            latest.Mask |= bit;
            latest.OriginalMask |= bit;
        }

        // Low four bits are the top-row picks, high four bits the bottom row.
        // Zero means no reactivation, rather than an OPEN pick.
        public int Release(int row, int pick) => Release(row, pick, out _);

        public int Release(int row, int pick, out Gesture released)
        {
            released = null;
            int bit = 1 << (row * 4 + pick);
            int index = gestures.FindIndex(g => (g.Mask & bit) != 0);
            if (index < 0) return 0;
            // Re-pressing a released member is a fresh gesture, not an extra
            // member of the previously assembled chord.
            // Moving a finger from an older anchor into the new chord does
            // not interrupt the new chord's still-open assembly window.
            if (index == gestures.Count - 1) gestures[index].CanJoin = false;
            var gesture = gestures[index];
            gesture.Mask &= ~bit;
            if (gesture.Mask != 0) return 0;
            bool wasLatest = index == gestures.Count - 1;
            gestures.RemoveAt(index);
            if (!wasLatest || gestures.Count == 0) return 0;
            released = gesture;
            return gestures[gestures.Count - 1].Mask;
        }

        public void Reset() => gestures.Clear();
    }
}
