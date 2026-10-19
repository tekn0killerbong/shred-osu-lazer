using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Input;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Objects.Drawables;

namespace osu.Game.Rulesets.Shred.UI
{
    public partial class ShredPlayfield
    {
        private readonly Dictionary<DrawableShredHitObject, (double time, bool picked)> chordInputs = new();
        private double completedPressTime = double.NegativeInfinity;
        private double completedPressWindow;
        private int completedPressKeys;
        private int fullMembers;
        private int fullAnchor;
        private int fullPressMask;
        private bool fullReturning;
        private bool completingFullStrum;
        private double fullPickTime;
        private DrawableShredHitObject[] fullTarget;
        private int heldPickMask => strings[0].PickMask | (strings[1].PickMask << 4);

        private bool freeHammerPull(DrawableShredHitObject note)
            => chordInputs.TryGetValue(note, out var input) && !input.picked;

        private DrawableShredHitObject[] chordAt(DrawableShredHitObject note)
            => AllHitObjects.OfType<DrawableShredHitObject>()
                .Where(n => !n.HitObject.IsWham && ShredChordRules.SameChordTime(n.HitObject.StartTime, note.HitObject.StartTime))
                .OrderBy(n => n.HitObject.StartTime).ThenBy(n => n.HitObject.Column).ToArray();

        private DrawableShredHitObject targetNote(IEnumerable<DrawableShredHitObject> source)
        {
            var pending = source.Where(n => !n.HitObject.IsWham && !n.HeadJudged)
                .OrderBy(n => n.HitObject.StartTime).ToArray();
            // An unrecoverable head awaiting its automatic miss must not lock
            // the following playable chord. Inputs in either outer band miss.
            return pending.FirstOrDefault(n => n.HitObject.TimingWindows.ResultFor(n.HeadOffset).IsHit())
                ?? pending.FirstOrDefault(n => ShredInputJudgement.IsWrongPickInWindow(n.HitObject.TimingWindows, n.HeadOffset));
        }

        public bool RequestChordHit(DrawableShredHitObject note, double time, bool picked)
        {
            if (note.HeadJudged || !note.HitObject.TimingWindows.ResultFor(time - note.HitObject.StartTime).IsHit()) return false;
            bool fresh = !chordInputs.TryGetValue(note, out var existing);
            if (fresh || !picked) chordInputs[note] = (time, picked);
            var chord = chordAt(note);
            if (chord.Any(n => n.HeadJudged && !n.HeadHit)) return false;
            if (chord.Any(n => !n.HeadJudged && !chordInputs.ContainsKey(n))) return fresh;
            double judgementTime = chord.Where(chordInputs.ContainsKey).Max(n => chordInputs[n].time);
            if (chord.Any(n => !n.HitObject.TimingWindows.ResultFor(judgementTime - n.HitObject.StartTime).IsHit())) return fresh;
            rememberCompletedChord(chord);
            // Commit only after every member, including the other row, is ready.
            foreach (var member in chord.Where(n => !n.HeadJudged))
                member.CommitHead(judgementTime, chordInputs[member].picked);
            for (int row = 0; row < 2; row++)
            {
                if (armed[row]?.Intersect(chord).Any() != true) continue;
                armed[row] = null;
                armedPickCount[row] = 0;
            }
            foreach (var member in chord) chordInputs.Remove(member);
            return fresh;
        }

        private double chordGestureWindow(DrawableShredHitObject[] chord)
        {
            double gap = AllHitObjects.OfType<DrawableShredHitObject>().Where(n => !n.HitObject.IsWham)
                .Select(n => Math.Abs(n.HitObject.StartTime - chord[0].HitObject.StartTime))
                .Where(g => g >= ShredChordRules.ChordTolerance).DefaultIfEmpty(double.PositiveInfinity).Min();
            return ShredPickBuffer<BufferedRowPick[]>.WindowFor(gap, Clock?.Rate ?? 1);
        }
        private void fencePhysicalChord(DrawableShredHitObject[] chord, int keys)
        {
            if (keys == 0) return;
            completedPressTime = Time.Current;
            completedPressWindow = chordGestureWindow(chord);
            completedPressKeys = keys;
        }
        private bool isExcessChordPress(int bit)
        {
            if (Time.Current < completedPressTime || Time.Current - completedPressTime > completedPressWindow
                || (completedPressKeys & bit) != 0) return false;
            completedPressKeys |= bit;
            return true;
        }
        private void rememberCompletedChord(DrawableShredHitObject[] chord)
        {
            bool regular = chord.All(n => n.HitObject.Fret > 0 && n.HitObject.Articulation == ShredPracticeArticulation.Pick);
            int physical = 0;
            for (int row = 0; row < 2; row++)
            {
                var members = chord.Where(n => n.HitObject.Row == row).ToArray();
                if (members.Length == 0 || armed[row]?.Intersect(members).Any() != true || armedReturn[row]) continue;
                physical |= armedPhysicalPicks[row];
                if (!regular) continue;
                int required = members.Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
                int picks = pickCountForRow(armedPhysicalPicks[row], row);
                if (strumCapacity(row, members, picks, armedGesture[row], armedPartner[row], false) < members.Length) continue;
                strumHistory.Remember(row, required);
                pickSequence.RememberFrets(row, required, members[0].HitObject.StartTime, armedPhysicalPicks[row]);
                physical |= armedPhysicalPicks[row];
            }
            if (!completingFullStrum) fencePhysicalChord(chord, physical);
            if (!regular || completingFullStrum || fullMembers != 0 || chord.Length < 2
                || BitOperations.PopCount((uint)physical) != chord.Length || heldPickMask != physical) return;
            fullMembers = physical;
            fullAnchor = 0;
            fullReturning = false;
        }

        public void ExpireChord(ShredHitObject note)
        {
            var drawable = AllHitObjects.OfType<DrawableShredHitObject>().FirstOrDefault(n => n.HitObject == note);
            if (drawable != null) ExpireChord(drawable);
        }
        public void ExpireChord(DrawableShredHitObject note)
        {
            var chord = chordAt(note);
            if (chord.Any(HasPendingPick)) return;
            if (chord.Any(n => !n.HeadJudged && Time.Current < n.HitObject.StartTime + n.HitObject.TimingWindows.WindowFor(HitResult.Miss))) return;
            missChord(chord, Time.Current);
        }
        private void missChord(DrawableShredHitObject[] chord, double time)
        {
            if (chord.Length > 0) chord = chordAt(chord[0]);
            foreach (var note in chord)
            {
                note.MissHead(time);
                chordInputs.Remove(note);
            }
            for (int row = 0; row < 2; row++)
                if (armed[row]?.Intersect(chord).Any() == true) { armed[row] = null; armedPickCount[row] = 0; }
        }

        private void resetFullStrum()
        {
            fullMembers = fullAnchor = fullPressMask = 0;
            fullReturning = false;
            fullTarget = null;
        }
        private void resetChordInput()
        {
            chordInputs.Clear();
            completedPressTime = double.NegativeInfinity;
            completedPressKeys = 0;
            resetFullStrum();
            Array.Clear(armed, 0, armed.Length);
            Array.Clear(armedPickCount, 0, armedPickCount.Length);
        }

        private bool fullStrumRelease(int released, int held)
        {
            if (fullMembers == 0) return false;
            if (drawableRuleset.IsPaused.Value || (fullAnchor & released) != 0 || (held & fullMembers) == 0)
            {
                resetFullStrum();
                return true;
            }
            if (fullReturning)
            {
                // An incomplete mobile group cannot acquire a free return.
                fullPressMask &= ~released;
                return true;
            }
            if ((released & fullMembers) == 0) return false;
            // A complete physical attack can miss its frets without invalidating
            // the next release. Do not carry its unfinished target into that release.
            fullTarget = null;
            int remaining = held & fullMembers;
            if (BitOperations.PopCount((uint)remaining) != 1) return true;
            if (fullAnchor == 0) fullAnchor = remaining;
            if (remaining != fullAnchor) return true;
            fullReturning = true;
            fullPressMask = 0;
            fullTarget = null;
            queueFullStrumReturn();
            return true;
        }

        private bool tryFullStrumPress(int bit)
        {
            if (fullMembers == 0) return false;
            if (!fullReturning || (heldPickMask & fullAnchor) == 0)
            {
                resetFullStrum();
                return false;
            }
            var candidate = targetNote(AllHitObjects.OfType<DrawableShredHitObject>());
            if (fullTarget != null && (fullTarget.All(n => n.HeadJudged)
                || (candidate != null && !fullTarget.Contains(candidate)
                    && fullTarget.All(n => n.HeadOffset > n.HitObject.TimingWindows.WindowFor(HitResult.Great)))))
            {
                fullTarget = null;
                fullPressMask = 0;
            }
            if (fullTarget == null)
            {
                var next = candidate;
                if (next != null)
                {
                    fullTarget = chordAt(next);
                    fullPressMask = 0;
                }
            }
            fullPressMask |= bit;
            fullPickTime = Time.Current;
            if (fullTarget == null)
            {
                // Keep the physical strum phase even when an attack falls
                // outside every judgement window. It cannot fund a later attack.
                if (BitOperations.PopCount((uint)fullPressMask) >= Math.Max(1, BitOperations.PopCount((uint)fullMembers) - 1))
                {
                    fullMembers = fullAnchor | fullPressMask;
                    fullReturning = false;
                }
                return true;
            }
            if (fullTarget.Any(n => n.HitObject.Articulation != ShredPracticeArticulation.Pick))
            {
                resetFullStrum();
                return false;
            }
            completeFullStrumPress();
            return true;
        }

        private DrawableShredHitObject[] eligibleFullChord(DrawableShredHitObject[] chord)
            => Enumerable.Range(0, 2).SelectMany(row => eligibleChordMembers(row, chord.Where(n => n.HitObject.Row == row).ToArray()))
                .ToArray();

        private void completeFullStrumPress()
        {
            if (fullTarget == null || fullTarget.Any(n => n.HeadJudged)) return;
            int needed = Math.Max(1, fullTarget.Length - 1);
            if (BitOperations.PopCount((uint)fullPressMask) < needed || (heldPickMask & fullPressMask) != fullPressMask) return;
            fullMembers = fullAnchor | fullPressMask;
            fullReturning = false;
            if (fullTarget.Any(n => n.HitObject.TimingWindows.ResultFor(n.HeadOffset) == HitResult.Miss))
            {
                missChord(fullTarget, Time.Current);
                fullTarget = null;
                return;
            }
            if (eligibleFullChord(fullTarget).Length != fullTarget.Length) return;
            var chord = fullTarget;
            fullTarget = null;
            fencePhysicalChord(chord, fullMembers);
            completingFullStrum = true;
            try { foreach (var note in chord) note.TryHit(fullPickTime, picked: true); }
            finally { completingFullStrum = false; }
        }

        private void queueFullStrumReturn()
        {
            var notes = AllHitObjects.OfType<DrawableShredHitObject>().Where(n => !n.HitObject.IsWham).ToArray();
            foreach (var note in notes) note.CheckExpiry();
            var next = targetNote(notes);
            if (next == null) return;
            var chord = chordAt(next);
            if (chord.Any(n => n.HitObject.Articulation != ShredPracticeArticulation.Pick)) return;
            var rows = new List<BufferedRowPick>();
            for (int row = 0; row < 2; row++)
            {
                var members = chord.Where(n => n.HitObject.Row == row).ToArray();
                if (members.Length == 0) continue;
                var gesture = new BufferedRowPick { Row = row, Chord = members, Count = members.Length, AnchorMask = fullAnchor,
                    Miss = members.Any(n => n.HitObject.TimingWindows.ResultFor(n.HeadOffset) == HitResult.Miss) };
                captureFrets(gesture);
                rows.Add(gesture);
            }
            double gap = notes.Select(n => Math.Abs(n.HitObject.StartTime - next.HitObject.StartTime))
                .Where(g => g >= ShredChordRules.ChordTolerance).DefaultIfEmpty(double.PositiveInfinity).Min();
            pickBuffer.Queue(Time.Current, ShredPickBuffer<BufferedRowPick[]>.WindowFor(gap, Clock?.Rate ?? 1), rows.ToArray());
        }
    }
}
