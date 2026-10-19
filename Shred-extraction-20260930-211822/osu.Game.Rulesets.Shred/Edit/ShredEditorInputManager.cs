using osu.Framework.Input.Bindings;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Shred.Edit
{
	public partial class ShredEditorInputManager : RulesetInputManager<ShredEditorAction>
	{
		public ShredEditorInputManager(RulesetInfo ruleset)
			: base(ruleset, Ruleset.EDITOR_VARIANT, SimultaneousBindingMode.Unique)
		{
		}
	}
}
