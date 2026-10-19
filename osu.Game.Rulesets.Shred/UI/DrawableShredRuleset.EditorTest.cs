using osu.Framework.Allocation;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Screens.Edit.GameplayTest;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Shred.UI
{
    public partial class DrawableShredRuleset
    {
        [Resolved(canBeNull: true)] private Player testPlayer { get; set; }
        [Resolved(canBeNull: true)] private GameplayClockContainer testClock { get; set; }

        protected override void LoadAsyncComplete()
        {
            base.LoadAsyncComplete();

            if (!(testPlayer is EditorPlayer) || testClock == null)
                return;

            // EditorPlayer separately credits every judgement before its starting
            // cursor. Its usage hook misses Shred's already-created, non-pooled
            // drawables. Mark their entries before the first update, without
            // emitting another score result, or they miss and get counted twice.
            // StartTime is the editor cursor (unaffected by gameplay offsets).
            foreach (var drawable in Playfield.AllHitObjects)
                markPrevious(drawable, testClock.StartTime);
        }

        private static void markPrevious(DrawableHitObject drawable, double cutoff)
        {
            foreach (var nested in drawable.NestedHitObjects)
                markPrevious(nested, cutoff);

            // A sustain crossing the cursor keeps its tail playable, while its
            // earlier head is already credited by EditorPlayer.
            if (drawable.Result == null || drawable.HitObject.GetEndTime() >= cutoff)
                return;

            drawable.Result.Type = drawable.Result.Judgement.MaxResult;
        }
    }
}
