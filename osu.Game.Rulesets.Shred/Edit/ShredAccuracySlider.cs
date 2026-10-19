using osu.Framework.Graphics.Cursor;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Shred.Scoring;

namespace osu.Game.Rulesets.Shred.Edit
{
    public partial class ShredAccuracySlider : FormSliderBar<float>, IHasCustomTooltip<RulesetBeatmapAttribute>
    {
        private double displayedValue = double.NaN;
        private RulesetBeatmapAttribute attribute;
        public ShredAccuracySlider()
        {
            // The parent provides the native rich tooltip over the whole control.
            TooltipFormat = _ => default;
        }
        public RulesetBeatmapAttribute TooltipContent
        {
            get
            {
                if (attribute == null || displayedValue != Current.Value)
                {
                    displayedValue = Current.Value;
                    attribute = ShredAccuracyDisplay.Create(displayedValue, displayedValue);
                }
                return attribute;
            }
        }
        public ITooltip<RulesetBeatmapAttribute> GetCustomTooltip() => new BeatmapAttributeTooltip();
    }
}
