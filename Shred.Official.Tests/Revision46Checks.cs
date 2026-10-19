using osu.Framework.Input.Bindings;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Compatibility;

internal static class Revision46Checks
{
	private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS v46 " + label); }
	public static void Run()
	{
		var defaults = new ShredRuleset().GetDefaultKeyBindings(Ruleset.EDITOR_VARIANT).ToArray();
		Check(defaults.Where(b => (ShredEditorAction)b.Action == ShredEditorAction.IncreaseGridType).Last().KeyCombination.Equals(new KeyCombination(InputKey.R))
			&& defaults.Where(b => (ShredEditorAction)b.Action == ShredEditorAction.DecreaseGridType).Last().KeyCombination.Equals(new KeyCombination(InputKey.E)), "corrected defaults: R increases type, E decreases type");
		using (var previousStorage = new TemporaryNativeStorage("shred-v46-type-correction"))
		using (var previousRealm = new RealmAccess(previousStorage, "migration.realm"))
		{
			using (var writer = new StreamWriter(previousStorage.GetStream("shred-migrations.ini", FileAccess.Write, FileMode.Create)))
				writer.Write("GridCommandsV45 = True\nSoundShortcutsV46 = True\n");
			previousRealm.Write(r =>
			{
				foreach (var b in defaults)
				{
					var key = b.KeyCombination;
					if (key.Equals(new KeyCombination(InputKey.R))) key = new KeyCombination(InputKey.E);
					else if (key.Equals(new KeyCombination(InputKey.E))) key = new KeyCombination(InputKey.R);
					r.Add(new RealmKeyBinding(b.Action, key, "shred", Ruleset.EDITOR_VARIANT));
				}
			});
			ShredGridBindingMigration.Apply(previousRealm, previousStorage);
			previousRealm.Run(r =>
			{
				foreach (var action in new[] { ShredEditorAction.IncreaseGridType, ShredEditorAction.DecreaseGridType })
					Check(r.All<RealmKeyBinding>().Where(b => b.ActionInt == (int)action).AsEnumerable().Select(b => b.KeyCombinationString).ToHashSet().SetEquals(defaults.Where(b => (ShredEditorAction)b.Action == action).Select(b => b.KeyCombination.ToString())), "already-installed v46 secondary pair corrected: " + action);
			});
			previousRealm.Write(r => r.All<RealmKeyBinding>().Single(b => b.ActionInt == (int)ShredEditorAction.IncreaseGridType && b.KeyCombinationString == new KeyCombination(InputKey.R).ToString()).KeyCombination = new KeyCombination(InputKey.F7));
			ShredGridBindingMigration.Apply(previousRealm, previousStorage);
			previousRealm.Run(r => Check(r.All<RealmKeyBinding>().Any(b => b.ActionInt == (int)ShredEditorAction.IncreaseGridType && b.KeyCombinationString == new KeyCombination(InputKey.F7).ToString()), "correction preserves subsequent custom binding"));
		}
		using var storage = new TemporaryNativeStorage("shred-v46-migration");
		using var realm = new RealmAccess(storage, "migration.realm");
		var globalDefaults = GlobalActionContainer.GetDefaultBindingsFor(GlobalActionCategory.Editor).ToArray();
		realm.Write(r =>
		{
			foreach (var b in defaults) r.Add(new RealmKeyBinding(b.Action, b.KeyCombination, "shred", Ruleset.EDITOR_VARIANT));
			foreach (var b in r.All<RealmKeyBinding>())
			{
				if (b.KeyCombination.Equals(new KeyCombination(InputKey.I))) b.KeyCombination = new KeyCombination(InputKey.O);
				else if (b.KeyCombination.Equals(new KeyCombination(InputKey.O))) b.KeyCombination = new KeyCombination(InputKey.I);
			}
			foreach (var b in globalDefaults) r.Add(new RealmKeyBinding(b.Action, b.KeyCombination));
			r.Add(new RealmKeyBinding(GlobalAction.EditorToggleWhistleSound, new KeyCombination(InputKey.F7)));
		});
		ShredGridBindingMigration.Apply(realm, storage);
		realm.Run(r =>
		{
			foreach (var action in new[] { ShredEditorAction.DecreaseGridSpacing, ShredEditorAction.IncreaseGridSpacing, ShredEditorAction.IncreaseGridType, ShredEditorAction.DecreaseGridType })
				Check(r.All<RealmKeyBinding>().Where(b => b.RulesetName == "shred" && b.ActionInt == (int)action).AsEnumerable().Select(b => b.KeyCombinationString).ToHashSet().SetEquals(defaults.Where(b => (ShredEditorAction)b.Action == action).Select(b => b.KeyCombination.ToString())), "upgrade from v44 migrates primary and retains secondary: " + action);
			Check(r.All<RealmKeyBinding>().Count(b => b.RulesetName == null && b.KeyCombinationString == new KeyCombination(InputKey.None).ToString()) == 12 + globalDefaults.Count(b => b.KeyCombination.Equals(new KeyCombination(InputKey.None))), "all twelve default combo/hitsound shortcuts cleared");
			Check(r.All<RealmKeyBinding>().Any(b => b.RulesetName == null && b.ActionInt == (int)GlobalAction.EditorToggleWhistleSound && b.KeyCombinationString == new KeyCombination(InputKey.F7).ToString()), "custom hitsound shortcut preserved");
			Check(r.All<RealmKeyBinding>().Single(b => b.RulesetName == null && b.ActionInt == (int)GlobalAction.EditorToggleRotateControl).KeyCombination.Equals(new KeyCombination(new[] { InputKey.Control, InputKey.R })), "Ctrl-R rotation remains bound");
		});
		realm.Write(r => r.All<RealmKeyBinding>().Single(b => b.RulesetName == null && b.ActionInt == (int)GlobalAction.EditorToggleClapSound).KeyCombination = new KeyCombination(InputKey.R));
		ShredGridBindingMigration.Apply(realm, storage);
		realm.Run(r => Check(r.All<RealmKeyBinding>().Single(b => b.RulesetName == null && b.ActionInt == (int)GlobalAction.EditorToggleClapSound).KeyCombination.Equals(new KeyCombination(InputKey.R)), "one-time unbinding preserves subsequent user changes"));
	}
}
