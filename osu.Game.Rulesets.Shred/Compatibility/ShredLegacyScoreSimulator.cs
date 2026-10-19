#if SHRED_OFFICIAL
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring.Legacy;

namespace osu.Game.Rulesets.Shred.Compatibility
{
    // Shred has no osu!stable scores. This fulfils the editor compatibility
    // interface without claiming standard score or performance compatibility.
    internal sealed class ShredLegacyScoreSimulator : ILegacyScoreSimulator
    {
        public LegacyScoreAttributes Simulate(IWorkingBeatmap workingBeatmap, IBeatmap playableBeatmap)
            => new LegacyScoreAttributes { AccuracyScore = 300 * playableBeatmap.HitObjects.Count,
                MaxCombo = playableBeatmap.HitObjects.Count, BonusScoreRatio = 1 };
        public double GetLegacyScoreMultiplier(IReadOnlyList<Mod> mods, LegacyBeatmapConversionDifficultyInfo difficulty) => 1;
    }
}
#endif
