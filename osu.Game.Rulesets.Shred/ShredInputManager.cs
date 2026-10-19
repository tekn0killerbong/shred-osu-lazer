using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using osu.Game.Input.Bindings;
using osu.Game.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.Rulesets.UI;
namespace osu.Game.Rulesets.Shred
{
	public partial class ShredInputManager : RulesetInputManager<ShredAction>
	{
		public ShredInputManager(RulesetInfo ruleset) : base(ruleset, 0, SimultaneousBindingMode.Unique) { }
		protected override KeyBindingContainer<ShredAction> CreateKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
			=> new ShredKeyBindingContainer(ruleset, variant, unique);

		public partial class ShredKeyBindingContainer : RulesetKeyBindingContainer
		{
			public ShredKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique) : base(ruleset, variant, unique) { }
			protected override bool HandleRepeats => false;
            protected override bool Prioritised => true;
            private readonly HashSet<ShredAction> keyboardActions = new HashSet<ShredAction>();
            private int diagnosticEvents;

            // Match every configured keyboard binding independently. In particular,
            // another pick must not release an action whose physical keys remain down.
            // Replay and non-keyboard input continue through the native implementation.
            protected override bool Handle(UIEvent e)
            {
                if (e is not KeyDownEvent && e is not KeyUpEvent) return base.Handle(e);
                if (e is KeyDownEvent repeat && repeat.Repeat)
                    return KeyBindings?.Any(b => b.KeyCombination.Keys.Contains(KeyCombination.FromKey(repeat.Key))) == true;
                var down = KeyCombination.FromInputState(e.CurrentState, null).Keys;
                var active = (KeyBindings ?? Array.Empty<IKeyBinding>())
                    .Where(b => b.KeyCombination.Keys.Length > 0 && b.KeyCombination.Keys.All(down.Contains))
                    .Select(b => (ShredAction)Convert.ToInt32(b.Action)).ToHashSet();
                traceKeyboard(e, active, down);
                bool handled = false;
                foreach (var action in keyboardActions.Except(active).ToArray())
                {
                    PropagateReleased(KeyBindingInputQueue, e.CurrentState, action);
                    keyboardActions.Remove(action);
                }
                foreach (var action in active.Except(keyboardActions).OrderBy(a => a))
                {
                    handled |= PropagatePressed(KeyBindingInputQueue, e.CurrentState, action) != null;
                    keyboardActions.Add(action);
                }
                return handled;
            }
            private void traceKeyboard(UIEvent e, HashSet<ShredAction> active, IEnumerable<InputKey> down)
            {
                // Temporary v34 diagnostic: only configured gameplay keys, bounded per session.
                if (diagnosticEvents >= 128 || e is not KeyboardEvent keyboard) return;
                var bindings = (KeyBindings ?? Array.Empty<IKeyBinding>()).ToArray();
                var mappedKeys = bindings.SelectMany(b => b.KeyCombination.Keys).ToHashSet();
                InputKey changed = KeyCombination.FromKey(keyboard.Key);
                if (!mappedKeys.Contains(changed)) return;
                if (diagnosticEvents++ == 0)
                    Logger.Log("Shred input v34 mappings: " + string.Join("; ", bindings.Select(b => $"{b.Action}={b.KeyCombination}")), LoggingTarget.Runtime, LogLevel.Verbose);
                Logger.Log($"Shred input v34 {(e is KeyDownEvent ? "down" : "up")} {changed}; held=[{string.Join(",", down.Where(mappedKeys.Contains))}]; matched=[{string.Join(",", active.OrderBy(a => a))}]; previous=[{string.Join(",", keyboardActions.OrderBy(a => a))}]", LoggingTarget.Runtime, LogLevel.Verbose);
            }
			protected override void ReloadMappings(IQueryable<RealmKeyBinding> realmKeyBindings)
			{
				// Filter legacy actions before lazer removes duplicate key combinations.
				base.ReloadMappings(realmKeyBindings.Where(binding => binding.ActionInt >= 100 && binding.ActionInt <= 120));
				KeyBindings = ShredBindingProfile.Resolve(KeyBindings);

			}
		}
	}
	public static class ShredBindingProfile
	{
		public static bool TryDecode(ShredAction value, out int row, out int action)
		{
			int index = (int)value - 100;
			row = -1;
			action = -1;
			if (index < 0 || index >= 18) return false;
			row = index / 9;
			action = index % 9;
			return true;
		}

		public static List<IKeyBinding> Resolve(IEnumerable<IKeyBinding> stored)
		{
			List<IKeyBinding> result = stored.Where(binding =>
			{
				int action = Convert.ToInt32(binding.Action);
				return action >= 100 && action <= 120;
			}).ToList();
			foreach (KeyBinding binding in new ShredRuleset().GetDefaultKeyBindings())
			{
				if (!result.Any(existing => Convert.ToInt32(existing.Action) == Convert.ToInt32(binding.Action))
					&& !result.Any(existing => existing.KeyCombination.Equals(binding.KeyCombination)))
					result.Add(binding);
			}
			return result;
		}
	}

	public enum ShredAction
	{
		[Description("Row 1 Fret 1")]
		Row1Fret1 = 100,
		[Description("Row 1 Fret 2")]
		Row1Fret2 = 101,
		[Description("Row 1 Fret 3")]
		Row1Fret3 = 102,
		[Description("Row 1 Fret 4")]
		Row1Fret4 = 103,
		[Description("Row 1 Fret 5")]
		Row1Fret5 = 104,
		[Description("Row 1 Pick 1")]
		Row1Pick1 = 105,
		[Description("Row 1 Pick 2")]
		Row1Pick2 = 106,
		[Description("Row 1 Pick 3")]
		Row1Pick3 = 107,
		[Description("Row 1 Pick 4")]
		Row1Pick4 = 108,
		[Description("Row 2 Fret 1")]
		Row2Fret1 = 109,
		[Description("Row 2 Fret 2")]
		Row2Fret2 = 110,
		[Description("Row 2 Fret 3")]
		Row2Fret3 = 111,
		[Description("Row 2 Fret 4")]
		Row2Fret4 = 112,
		[Description("Row 2 Fret 5")]
		Row2Fret5 = 113,
		[Description("Row 2 Pick 1")]
		Row2Pick1 = 114,
		[Description("Row 2 Pick 2")]
		Row2Pick2 = 115,
		[Description("Row 2 Pick 3")]
		Row2Pick3 = 116,
		[Description("Row 2 Pick 4")]
		Row2Pick4 = 117,
		[Description("Increase scroll speed")]
		ScrollFaster = 118,
		[Description("Decrease scroll speed")]
		ScrollSlower = 119,
		[Description("Whammy bar")]
		Wham = 120,
	}
}
