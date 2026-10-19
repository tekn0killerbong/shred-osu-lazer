using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;

namespace osu.Game.Rulesets.Shred.Beatmaps
{
    public static class ShredCoincidentHeads
    {
        // Old paste operations could leave sub-millisecond LN remnants beside
        // the intended head. Repair the detached playable copy, never the file.
        public static List<ShredHitObject> RemoveDuplicates(IEnumerable<ShredHitObject> source)
        {
            var removed = new HashSet<ShredHitObject>();
            var notes = source.ToArray();
            foreach (var lane in notes.GroupBy(n => n.IsWham ? 12 : n.Column))
            {
                ShredHitObject kept = null;
                double previousTime = double.NegativeInfinity;
                foreach (var note in lane.OrderBy(n => n.StartTime))
                {
                    if (kept != null && ShredChordRules.SameChordTime(previousTime, note.StartTime))
                    {
                        if (note.Duration > kept.Duration) { removed.Add(kept); kept = note; }
                        else removed.Add(note);
                    }
                    else kept = note;
                    previousTime = note.StartTime;
                }
            }
            return notes.Where(n => !removed.Contains(n)).OrderBy(n => n.StartTime).ToList();
        }
    }
}
