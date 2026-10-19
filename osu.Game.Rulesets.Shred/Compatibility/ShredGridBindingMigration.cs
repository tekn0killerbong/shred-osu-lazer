using System.Linq;
using osu.Framework.Configuration;
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Shred.Edit;

namespace osu.Game.Rulesets.Shred.Compatibility
{
	/// <summary>Migrate former defaults once; preserve custom commands and their action IDs.</summary>
	public static class ShredGridBindingMigration
	{
		private static readonly object sync = new object();

		public static void Apply(RealmAccess realm, Storage storage)
		{
			lock (sync)
			{
				using var config = new MigrationConfig(storage);
				bool gridDone = config.Get<bool>(MigrationSetting.GridCommandsV45);
				bool soundsDone = config.Get<bool>(MigrationSetting.SoundShortcutsV46);
				bool typesDone = config.Get<bool>(MigrationSetting.SecondaryTypesV46Corrected);
				bool editorDone = config.Get<bool>(MigrationSetting.EditorBindingsV62);
				if (gridDone && soundsDone && typesDone && editorDone) return;
				bool found = false;
				bool globalFound = false;
				realm.Write(r =>
				{
					var bindings = r.All<RealmKeyBinding>()
						.Where(b => b.RulesetName == "shred" && b.Variant == Ruleset.EDITOR_VARIANT).ToArray();
					if (bindings.Length == 0) return;
					found = true;
					if (!typesDone) CorrectSecondaryTypes(bindings);
					if (!gridDone)
					{
						SwapFormerDefaults(bindings, ShredEditorAction.DecreaseGridSpacing, ShredEditorAction.IncreaseGridSpacing,
							InputKey.O, InputKey.I);
						SwapFormerDefaults(bindings, ShredEditorAction.IncreaseGridType, ShredEditorAction.DecreaseGridType,
							Input.ShredDefaultKeys.ModeIncrease, InputKey.P);
					}
					if (!soundsDone)
					{
						var globals = r.All<RealmKeyBinding>().Where(b => b.RulesetName == null && b.Variant == null).ToArray();
						globalFound = globals.Length > 0;
						ClearFormerSoundDefaults(globals);
					}
					if (!editorDone)
					{
						ShredEmptyStepBindings.Apply(r);
						UpdateEditorDefaults(bindings);
					}
				});
				if (found) config.SetValue(MigrationSetting.GridCommandsV45, true);
				if (found) config.SetValue(MigrationSetting.SecondaryTypesV46Corrected, true);
				if (found) config.SetValue(MigrationSetting.EditorBindingsV62, true);
				if (globalFound) config.SetValue(MigrationSetting.SoundShortcutsV46, true);
			}
		}

		public static void SwapFormerDefaults(RealmKeyBinding[] bindings, ShredEditorAction increase, ShredEditorAction decrease,
			InputKey oldIncrease, InputKey oldDecrease)
		{
			// Historical migration must use its historical secondary keys, not
			// the current defaults (which now have empty secondary slots).
			InputKey formerSecondary(ShredEditorAction action) => action switch
			{
				ShredEditorAction.DecreaseGridSpacing => InputKey.Q,
				ShredEditorAction.IncreaseGridSpacing => InputKey.W,
				ShredEditorAction.IncreaseGridType => InputKey.R,
				_ => InputKey.E
			};
			var up = bindings.Where(b => b.ActionInt == (int)increase && !b.KeyCombination.Equals(new KeyCombination(formerSecondary(increase)))
				&& !b.KeyCombination.Equals(new KeyCombination(InputKey.None))).ToArray();
			var down = bindings.Where(b => b.ActionInt == (int)decrease && !b.KeyCombination.Equals(new KeyCombination(formerSecondary(decrease)))
				&& !b.KeyCombination.Equals(new KeyCombination(InputKey.None))).ToArray();
			if (up.Length != 1 || down.Length != 1) return;
			if (!up[0].KeyCombination.Equals(new KeyCombination(oldIncrease))
				|| !down[0].KeyCombination.Equals(new KeyCombination(oldDecrease))) return;
			up[0].KeyCombination = new KeyCombination(oldDecrease);
			down[0].KeyCombination = new KeyCombination(oldIncrease);
		}

		public static void ClearFormerSoundDefaults(RealmKeyBinding[] bindings)
		{
			var actions = new[]
			{
				GlobalAction.EditorToggleNewCombo, GlobalAction.EditorToggleWhistleSound,
				GlobalAction.EditorToggleFinishSound, GlobalAction.EditorToggleClapSound,
				GlobalAction.EditorToggleNormalAutoBank, GlobalAction.EditorToggleNormalNormalBank,
				GlobalAction.EditorToggleNormalSoftBank, GlobalAction.EditorToggleNormalDrumBank,
				GlobalAction.EditorToggleAdditionAutoBank, GlobalAction.EditorToggleAdditionNormalBank,
				GlobalAction.EditorToggleAdditionSoftBank, GlobalAction.EditorToggleAdditionDrumBank
			};
			var defaults = GlobalActionContainer.GetDefaultBindingsFor(GlobalActionCategory.Editor)
				.Where(b => actions.Contains((GlobalAction)b.Action)).ToArray();
			foreach (var binding in bindings)
				if (binding.RulesetName == null && binding.Variant == null
					&& defaults.Any(d => (int)(GlobalAction)d.Action == binding.ActionInt && d.KeyCombination.Equals(binding.KeyCombination)))
					binding.KeyCombination = new KeyCombination(InputKey.None);
		}

		public static void CorrectSecondaryTypes(RealmKeyBinding[] bindings)
		{
			var up = bindings.Where(b => b.ActionInt == (int)ShredEditorAction.IncreaseGridType).ToArray();
			var down = bindings.Where(b => b.ActionInt == (int)ShredEditorAction.DecreaseGridType).ToArray();
			if (up.Length != 2 || down.Length != 2) return;
			if (!up.Any(b => b.KeyCombination.Equals(new KeyCombination(InputKey.P)))
				|| !down.Any(b => b.KeyCombination.Equals(new KeyCombination(Input.ShredDefaultKeys.ModeIncrease)))) return;
			var oldUp = up.SingleOrDefault(b => b.KeyCombination.Equals(new KeyCombination(InputKey.E)));
			var oldDown = down.SingleOrDefault(b => b.KeyCombination.Equals(new KeyCombination(InputKey.R)));
			if (oldUp == null || oldDown == null) return;
			oldUp.KeyCombination = new KeyCombination(InputKey.R);
			oldDown.KeyCombination = new KeyCombination(InputKey.E);
		}

		public static void UpdateEditorDefaults(RealmKeyBinding[] bindings)
		{
			foreach (var (action, primary, secondary, replacement) in new[]
			{
				(ShredEditorAction.DecreaseGridSpacing, InputKey.I, InputKey.Q, InputKey.Number7),
				(ShredEditorAction.IncreaseGridSpacing, InputKey.O, InputKey.W, InputKey.Number8),
				(ShredEditorAction.DecreaseGridType, Input.ShredDefaultKeys.ModeIncrease, InputKey.E, InputKey.Number9),
				(ShredEditorAction.IncreaseGridType, InputKey.P, InputKey.R, InputKey.Number0)
			})
			{
				var slots = bindings.Where(b => b.IsValid && b.RulesetName == "shred" && b.Variant == Ruleset.EDITOR_VARIANT
					&& b.ActionInt == (int)action).ToArray();
				if (slots.Length > 0 && slots[0].KeyCombination.Equals(new KeyCombination(primary)))
					slots[0].KeyCombination = new KeyCombination(replacement);
				if (slots.Length > 1 && slots[1].KeyCombination.Equals(new KeyCombination(secondary)))
					slots[1].KeyCombination = new KeyCombination(InputKey.None);
			}
		}

		private enum MigrationSetting { GridCommandsV45, SoundShortcutsV46, SecondaryTypesV46Corrected, EditorBindingsV62 }
		private sealed class MigrationConfig : IniConfigManager<MigrationSetting>
		{
			protected override string Filename => "shred-migrations.ini";
			public MigrationConfig(Storage storage) : base(storage) { }
			protected override void InitialiseDefaults()
			{
				SetDefault(MigrationSetting.GridCommandsV45, false);
				SetDefault(MigrationSetting.SoundShortcutsV46, false);
				SetDefault(MigrationSetting.SecondaryTypesV46Corrected, false);
				SetDefault(MigrationSetting.EditorBindingsV62, false);
			}
		}
	}
}
