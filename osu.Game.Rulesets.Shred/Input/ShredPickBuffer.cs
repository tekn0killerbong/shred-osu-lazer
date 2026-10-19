using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Shred.Input
{
    // Defer only synthetic pulls. Physical presses are never debounced against
    // each other: every distinct pick in a chord must keep its own edge.
    public sealed class ShredPickBuffer<T>
    {
        public const double MaximumRealMilliseconds = 30;
        private readonly List<(double time, double window, T value)> pending = new List<(double, double, T)>();
        private double lastPhysicalTime = double.NegativeInfinity;
        public IEnumerable<T> PendingValues => pending.Select(p => p.value);

        public static double WindowFor(double nearestNoteGap, double rate = 1)
        {
            // Manual/uninitialised clocks can report zero while test code still
            // advances CurrentTime. That is not gameplay at 0.01x speed.
            // Pause/rewind cancellation is handled by the playfield separately.
            double effectiveRate = double.IsFinite(rate) && rate > 0 ? rate : 1;
            return Math.Min(MaximumRealMilliseconds * effectiveRate, nearestNoteGap / 4);
        }

        public bool Queue(double time, double window, T value)
        {
            if (time >= lastPhysicalTime && time - lastPhysicalTime <= window) return false;
            // Cascading releases in one gesture must not manufacture several
            // synthetic strums. Physical presses remain independent.
            if (pending.Count > 0)
            {
                var latest = pending[pending.Count - 1];
                if (time >= latest.time && time - latest.time <= Math.Min(window, latest.window)) return false;
            }
            pending.Add((time, window, value));
            return true;
        }

        public void PhysicalPress(double time, Action<T, double> commit)
        {
            pending.RemoveAll(p => time >= p.time && time - p.time <= p.window);
            Flush(time, commit);
            lastPhysicalTime = time;
        }

        public void Flush(double time, Action<T, double> commit)
        {
            // Strict boundary: an input exactly at the buffer boundary still belongs to the
            // same gesture, even if an update ran at that same timestamp.
            while (pending.Count > 0 && time - pending[0].time > pending[0].window)
            {
                var next = pending[0];
                pending.RemoveAt(0);
                commit(next.value, next.time);
            }
        }

        public void CancelWhere(Func<T, bool> predicate) => pending.RemoveAll(p => predicate(p.value));

        public void Reset() { pending.Clear(); lastPhysicalTime = double.NegativeInfinity; }
    }
}
