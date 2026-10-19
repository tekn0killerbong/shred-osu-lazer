using osu.Framework.Configuration;
using osu.Framework.Platform;

namespace osu.Game.Rulesets.Shred.Edit
{
    public enum ShredRecorderSetting { Enabled, ReturnOnPause }

    public class ShredRecorderConfig : IniConfigManager<ShredRecorderSetting>
    {
        protected override string Filename => "shred-editor.ini";
        public ShredRecorderConfig(Storage storage) : base(storage) { }
        protected override void InitialiseDefaults()
        {
            SetDefault(ShredRecorderSetting.Enabled, false);
            SetDefault(ShredRecorderSetting.ReturnOnPause, false);
        }
    }
}
