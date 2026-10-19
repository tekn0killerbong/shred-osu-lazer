using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.Input.Bindings;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Input;
using Realms;

namespace osu.Game.Rulesets.Shred.Edit
{
	public partial class ShredEditorSettings : RulesetSettingsSubsection
	{
		public ShredEditorSettings(Ruleset ruleset) : base(ruleset)
		{
			Add(new EditorSection(ruleset.RulesetInfo));
		}
		private partial class EditorSection : SettingsSubsection
		{
			protected override LocalisableString Header => "Editor";
			public EditorSection(RulesetInfo ruleset) => Add(new RecorderBindings(ruleset));
		}
		private partial class RecorderBindings : RulesetEditorBindingsSubsection
		{
			protected override LocalisableString Header => "Step recorder";
			public RecorderBindings(RulesetInfo ruleset) : base(ruleset)
			{
				Defaults = new ShredRuleset().GetDefaultKeyBindings(osu.Game.Rulesets.Ruleset.EDITOR_VARIANT)
					.Where(b => (int)(ShredEditorAction)b.Action >= 2);
			}
			protected override IEnumerable<RealmKeyBinding> GetKeyBindings(Realm realm)
				=> base.GetKeyBindings(realm).Where(b => b.ActionInt == 2 || b.ActionInt == 3 || b.ActionInt == 8 || b.ActionInt == 9 || b.ActionInt == 10);
		}
	}
}
