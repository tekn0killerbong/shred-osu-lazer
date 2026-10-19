using osu.Game.Graphics;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Shred.Scoring
{
    public static class ShredAccuracyDisplay
    {
        public const string Description = "Affects timing requirements for notes.";
        public static RulesetBeatmapAttribute Create(double original, double adjusted, double rate = 1)
        {
            var windows = new ShredHitWindows();
            windows.SetDifficulty(adjusted);
            var colours = new OsuColour();
            return new RulesetBeatmapAttribute(osu.Game.Localisation.SongSelectStrings.Accuracy, "OD", (float)original, (float)adjusted, 10)
            {
                Description = Description,
                AdditionalMetrics = new[]
                {
                    new RulesetBeatmapAttribute.AdditionalMetric("GREAT hit window", $"±{windows.WindowFor(HitResult.Great) / rate:0.##} ms", colours.ForHitResult(HitResult.Great)),
                    new RulesetBeatmapAttribute.AdditionalMetric("MISS hit window", $"±{windows.WindowFor(HitResult.Miss) / rate:0.##} ms", colours.ForHitResult(HitResult.Miss))
                }
            };
        }
    }
}
