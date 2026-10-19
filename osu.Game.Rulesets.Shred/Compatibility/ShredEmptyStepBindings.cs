using System.Linq;
using osu.Framework.Input.Bindings;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Shred.Edit;
using Realms;

namespace osu.Game.Rulesets.Shred.Compatibility
{
    /// <summary>Consolidate legacy actions into two slots; retain custom keys before redundant defaults.</summary>
    public static class ShredEmptyStepBindings
    {
        public static void Apply(Realm realm)
        {
            var bindings = realm.All<RealmKeyBinding>()
                .Where(b => b.RulesetName == "shred" && b.Variant == Ruleset.EDITOR_VARIANT).ToArray();
            var slots = bindings.Where(b => b.ActionInt == (int)ShredEditorAction.EmptyStep).ToList();
            var defaults = new ShredRuleset().GetDefaultKeyBindings(Ruleset.EDITOR_VARIANT)
                .Where(b => (ShredEditorAction)b.Action == ShredEditorAction.EmptyStep).ToArray();
            var alternate = bindings.FirstOrDefault(b => b.ActionInt == (int)ShredEditorAction.EmptyStepAlternate);
            var navigation = bindings.FirstOrDefault(b => b.ActionInt == (int)ShredEditorAction.EmptyStepNavigation);
            var keys = slots.Select(b => b.KeyCombination).ToList();
            if (keys.Count == 0) keys.Add(defaults[0].KeyCombination);
            if (alternate != null)
            {
                if (keys[0].Equals(new KeyCombination(InputKey.J))) keys[0] = defaults[0].KeyCombination;
                var key = alternate.KeyCombination;
                if (key.Equals(new KeyCombination(InputKey.N))) key = new KeyCombination(Input.ShredDefaultKeys.EmptyStepAlternate);
                if (keys.Count > 1) keys[1] = key;
                else keys.Add(key);
            }
            if (navigation != null) keys.Add(navigation.KeyCombination);

            // Lazer may already have trimmed the third slot before this upgrade.
            // The former second default (è) becomes Right. Prefer a custom
            // secondary over either old default when all three slots survive.
            var second = keys.Skip(1).Where(key => !key.Equals(new KeyCombination(Input.ShredDefaultKeys.EmptyStepAlternate))
                && !key.Equals(new KeyCombination(InputKey.Right))).DefaultIfEmpty(defaults[1].KeyCombination).First();
            var selected = new[] { keys[0], second };
            while (slots.Count < defaults.Length)
            {
                var binding = defaults[slots.Count];
                slots.Add(realm.Add(new RealmKeyBinding(binding.Action, binding.KeyCombination, "shred", Ruleset.EDITOR_VARIANT)));
            }
            for (int i = 0; i < selected.Length; i++) slots[i].KeyCombination = selected[i];
            foreach (var excess in slots.Skip(defaults.Length)) realm.Remove(excess);
            foreach (var legacy in bindings.Where(b => b.IsValid && (b.ActionInt == (int)ShredEditorAction.EmptyStepAlternate
                || b.ActionInt == (int)ShredEditorAction.EmptyStepNavigation))) realm.Remove(legacy);
        }
    }
}
