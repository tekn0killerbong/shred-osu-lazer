using osu.Framework.Allocation;
using osu.Game.Database;
using osu.Game.Overlays.Settings;

namespace osu.Game.Rulesets.Shred.Edit
{
	public partial class ShredEditorSettings : RulesetSettingsSubsection
	{
		public ShredEditorSettings(Ruleset ruleset) : base(ruleset)
		{
			Add(new ShredGameplaySettings());
			#if SHRED_OFFICIAL
            Add(new Compatibility.ShredNativeSettings());
#endif
		}
        [BackgroundDependencyLoader]
        private void load(RealmAccess realm, osu.Framework.Platform.Storage storage)
        {
            realm.Write(Compatibility.ShredScrollBindingMigration.Apply);
            Compatibility.ShredGridBindingMigration.Apply(realm, storage);
        }
	}
}
