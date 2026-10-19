using System.Linq;
using osu.Framework.Input.Bindings;
using osu.Game.Input.Bindings;
using Realms;

namespace osu.Game.Rulesets.Shred.Compatibility
{
    public static class ShredScrollBindingMigration
    {
        // Run when settings load, so both the displayed bindings and gameplay agree.
        // Only migrate our exact former defaults, preserving custom bindings.
        public static void Apply(Realm realm)
        {
            var bindings = realm.All<RealmKeyBinding>()
                .Where(b => b.RulesetName == "shred" && b.Variant == 0).ToArray();
            var faster = bindings.Where(b => b.ActionInt == (int)ShredAction.ScrollFaster).ToArray();
            var slower = bindings.Where(b => b.ActionInt == (int)ShredAction.ScrollSlower).ToArray();
            if (faster.Length != 1 || slower.Length != 1) return;
            if (!faster[0].KeyCombination.Equals(new KeyCombination(InputKey.F3))
                || !slower[0].KeyCombination.Equals(new KeyCombination(InputKey.F4))) return;
            faster[0].KeyCombination = new KeyCombination(InputKey.F4);
            slower[0].KeyCombination = new KeyCombination(InputKey.F3);
        }
    }
}
