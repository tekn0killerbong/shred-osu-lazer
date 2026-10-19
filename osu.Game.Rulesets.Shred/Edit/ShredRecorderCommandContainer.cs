using System;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Game.Database;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Bindings;
using osu.Game.Input.Bindings;

namespace osu.Game.Rulesets.Shred.Edit
{
    // Keep recorder commands inside their binding container so native modifier
    // matching and user custom bindings work even while recording is disabled.
    public partial class ShredRecorderCommandContainer : DatabasedKeyBindingContainer<ShredEditorAction>
    {
        public ShredRecorderCommandContainer(RulesetInfo ruleset)
            : base(ruleset, Ruleset.EDITOR_VARIANT, matchingMode: KeyCombinationMatchingMode.Modifiers) { }
        [BackgroundDependencyLoader]
        private void load(RealmAccess realm, Storage storage) => Compatibility.ShredGridBindingMigration.Apply(realm, storage);

        protected override bool Prioritised => true;
        public override IEnumerable<IKeyBinding> DefaultKeyBindings => base.DefaultKeyBindings
            .Where(b => Convert.ToInt32(b.Action) >= 10 && Convert.ToInt32(b.Action) <= 14);
        protected override void ReloadMappings(IQueryable<RealmKeyBinding> mappings)
        {
            base.ReloadMappings(mappings.Where(b => b.ActionInt >= 10 && b.ActionInt <= 14));
            var resolved = KeyBindings.ToList();
            foreach (var binding in DefaultKeyBindings)
                if (!resolved.Any(b => Convert.ToInt32(b.Action) == Convert.ToInt32(binding.Action))
                    && !resolved.Any(b => b.KeyCombination.Equals(binding.KeyCombination))) resolved.Add(binding);
            KeyBindings = resolved;
        }
    }
}
