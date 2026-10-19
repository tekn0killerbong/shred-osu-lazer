using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Bindings;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Shred.Edit
{
	/// <summary>Loads live osu! bindings without dispatching editor or gameplay actions.</summary>
	public partial class ShredRecorderBindingSource<T> : RulesetInputManager<T> where T : struct, Enum
	{
		public IReadOnlyList<IKeyBinding> Mappings { get; private set; } = Array.Empty<IKeyBinding>();
		public override bool PropagateNonPositionalInputSubTree => false;
		public ShredRecorderBindingSource(RulesetInfo ruleset, int variant)
			: base(ruleset, variant, SimultaneousBindingMode.Unique) { }
		protected override KeyBindingContainer<T> CreateKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode mode)
			=> new MappingContainer(this, ruleset, variant, mode);
		private partial class MappingContainer : RulesetKeyBindingContainer
		{
			private readonly ShredRecorderBindingSource<T> owner;
			private readonly int variant;
			public MappingContainer(ShredRecorderBindingSource<T> owner, RulesetInfo ruleset, int variant, SimultaneousBindingMode mode)
				: base(ruleset, variant, mode) { this.owner = owner; this.variant = variant; }
			protected override void ReloadMappings(IQueryable<RealmKeyBinding> mappings)
			{
				if (variant == Ruleset.EDITOR_VARIANT)
				{
					base.ReloadMappings(mappings.Where(b => b.ActionInt <= 2 || (b.ActionInt >= 10 && b.ActionInt <= 14)));
					var resolved = KeyBindings.ToList();
					foreach (KeyBinding binding in new ShredRuleset().GetDefaultKeyBindings(Ruleset.EDITOR_VARIANT))
					{
						if (!resolved.Any(b => Convert.ToInt32(b.Action) == Convert.ToInt32(binding.Action))
							&& !resolved.Any(b => b.KeyCombination.Equals(binding.KeyCombination))) resolved.Add(binding);
					}
					KeyBindings = resolved;
				}
				else
				{
					base.ReloadMappings(mappings.Where(b => b.ActionInt >= 100 && b.ActionInt <= 120));
					KeyBindings = ShredBindingProfile.Resolve(KeyBindings);
				}
				RealmKeyBindingStore.ClearDuplicateBindings(KeyBindings);
				owner.Mappings = KeyBindings.ToArray();
			}
		}
	}
}
