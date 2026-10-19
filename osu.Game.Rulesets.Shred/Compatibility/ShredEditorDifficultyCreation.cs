#if SHRED_OFFICIAL
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Localisation;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components.Menus;

namespace osu.Game.Rulesets.Shred.Compatibility
{
	internal static class ShredEditorDifficultyCreation
	{
		private static readonly MethodInfo createDifficulty = typeof(Editor).GetMethod("CreateNewDifficulty",
			BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(RulesetInfo) }, null);

		public static void Install(Editor editor)
		{
			EditorMenuBar menu = editor.ChildrenOfType<EditorMenuBar>().SingleOrDefault();
			RulesetInfo shred = editor.Dependencies.Get<RulesetStore>().GetRuleset("shred");
			if (menu == null || shred == null || createDifficulty == null) return;
			MenuItem creationMenu = descendants(menu.Items).SingleOrDefault(item => item.Text.Value.Equals(EditorStrings.CreateNewDifficulty));
			MenuItem action = creationMenu?.Items.SingleOrDefault(item => item.Text.Value.ToString() == shred.Name);
			if (action == null) return;

			// Match the editor's existing lossless mode-0 writer on a detached identity.
			// Realm saves the native ruleset by ShortName; the global catalogue stays untouched.
			RulesetInfo writableIdentity = shred.Clone();
			writableIdentity.OnlineID = 0;
			Action<RulesetInfo> create = createDifficulty.CreateDelegate<Action<RulesetInfo>>(editor);
			// Keep the native save prompts, empty/copy dialog, state and difficulty switch flow.
			action.Action.Value = () => create(writableIdentity);
		}

		private static IEnumerable<MenuItem> descendants(IEnumerable<MenuItem> items)
		{
			foreach (MenuItem item in items)
			{
				yield return item;
				foreach (MenuItem child in descendants(item.Items)) yield return child;
			}
		}
	}
}
#endif
