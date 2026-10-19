using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Shred.UI;

namespace osu.Game.Rulesets.Shred.Edit
{
    public partial class ShredGameplaySettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Gameplay";
        [BackgroundDependencyLoader]
        private void load()
        {
            Add(new SettingsItemV2(new FormSliderBar<int>
            {
                Caption = RulesetSettingsStrings.ScrollSpeed,
                Current = ShredScrollSpeed.Setting.GetBoundCopy(),
                KeyboardStep = 1,
                LabelFormat = speed => RulesetSettingsStrings.ScrollSpeedTooltip((int)(11485.0 / speed), speed)
            }));
        }
    }
}
