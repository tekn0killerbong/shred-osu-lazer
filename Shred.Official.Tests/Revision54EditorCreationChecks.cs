using osu.Framework.Allocation;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Localisation;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;
using System.Reflection;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.Components.Menus;
using osu.Game.Screens.Menu;

internal partial class Revision54EditorRateChecks
{
	private void checkPlaybackAlignment(Editor editor, string mode)
	{
		PlaybackPitchControl header = editor.ChildrenOfType<PlaybackPitchControl>().Single();
		PlaybackRateControl tabs = editor.ChildrenOfType<PlaybackRateControl>().Single();
		check(Math.Abs(header.ScreenSpaceDrawQuad.TopLeft.X - tabs.ScreenSpaceDrawQuad.TopLeft.X) < 0.1,
			mode + " percentages start at header left edge");
		check(Math.Abs(header.ScreenSpaceDrawQuad.BottomRight.X - tabs.ScreenSpaceDrawQuad.BottomRight.X) < 0.1,
			mode + " percentages end at toggle right edge");
	}

	private IEnumerator<Func<bool>> runCreationSteps()
	{
		yield return () => ScreenStack?.CurrentScreen is MainMenu;
		string archive = createFixture();
		Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(archive), "creation.osz") });
		yield return () => import.IsCompleted;
		import.GetAwaiter().GetResult();
		RealmAccess realm = Dependencies.Get<RealmAccess>();
		foreach (string mode in new[] { "osu", "taiko", "fruits", "mania", "shred" })
		{
			BeatmapInfo source = null;
			yield return () => (source = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-" + mode))?.Ruleset.ShortName == mode;
			string originalHash = source.Hash;
			Guid originalId = source.ID;
			Guid setId = source.BeatmapSet.ID;
			int initialCount = source.BeatmapSet.Beatmaps.Count;
			Beatmap.Value = BeatmapManager.GetWorkingBeatmap(source, refetch: true);
			Ruleset.Value = source.Ruleset;
			ScreenStack.Push(new EditorLoader());
			yield return () => ScreenStack.CurrentScreen is Editor ready && ready.ReadyForUse;
			Editor originalEditor = (Editor)ScreenStack.CurrentScreen;
			IDialogOverlay dialogs = originalEditor.Dependencies.Get<IDialogOverlay>();
			yield return () => true;
			checkPlaybackAlignment(originalEditor, mode);
			createShredItem(originalEditor).Action.Value.Invoke();
			if (mode == "shred")
			{
				yield return () => dialogs.CurrentDialog is CreateNewDifficultyDialog { IsLoaded: true };
				check(true, "same-ruleset new/copy dialog preserved");
				dialogs.CurrentDialog.Buttons.First().TriggerClick();
			}
			yield return () => ScreenStack.CurrentScreen is Editor ready && ready != originalEditor && ready.ReadyForUse;
			Editor createdEditor = (Editor)ScreenStack.CurrentScreen;
			EditorBeatmap created = createdEditor.Dependencies.Get<EditorBeatmap>();
			Guid createdId = created.BeatmapInfo.ID;
			check(created.BeatmapInfo.Ruleset.ShortName == "shred", mode + " menu creates Shred editor");
			check(createdId != originalId && created.BeatmapInfo.BeatmapSet.ID == setId, mode + " separate difficulty in same song");
			check(created.HitObjects.Count == 0 && createdEditor.ChildrenOfType<ShredHitObjectComposer>().Any(), mode + " empty Shred composition ready");
			check(created.Metadata.AudioFile == "tone.wav" && close(created.ControlPointInfo.TimingPoints.Single().BeatLength, 500), mode + " audio and timing preserved");
			BeatmapInfo saved = BeatmapManager.QueryBeatmap(b => b.ID == createdId);
			check(saved.Ruleset.ShortName == "shred" && saved.Ruleset.OnlineID == -1, mode + " database difficulty is native Shred");
			check(saved.BeatmapSet.Beatmaps.Count == initialCount + 1, mode + " exactly one new difficulty");
			BeatmapInfo unchanged = BeatmapManager.QueryBeatmap(b => b.ID == originalId);
			check(unchanged.Hash == originalHash && unchanged.Ruleset.ShortName == mode
				&& BeatmapManager.GetWorkingBeatmap(unchanged, refetch: true).Beatmap.HitObjects.Count == 50, mode + " source file and notes untouched");
			check(realm.Run(r => r.Find<RulesetInfo>("shred").OnlineID) == -1
				&& realm.Run(r => r.Find<RulesetInfo>("osu").OnlineID) == 0, mode + " catalogue identities untouched");
			created.Add(new ShredHitObject { StartTime = 6500, Row = 1, Fret = 2, Duration = 750 });
			yield return () => hasUnsavedChanges(createdEditor);
			if (mode == "osu")
			{
				// Cancelling the native save prompt must keep the edit and create nothing.
				createShredItem(createdEditor).Action.Value.Invoke();
				yield return () => dialogs.CurrentDialog is CreateNewDifficultyDialog { IsLoaded: true };
				dialogs.CurrentDialog.Buttons.First().TriggerClick();
				yield return () => dialogs.CurrentDialog is PromptForSaveDialog { IsLoaded: true };
				dialogs.CurrentDialog.Buttons.Last().TriggerClick();
				yield return () => dialogs.CurrentDialog == null;
				check(ScreenStack.CurrentScreen == createdEditor && created.HitObjects.Count == 1 && hasUnsavedChanges(createdEditor),
					"cancel creation keeps unsaved note in current editor");
				check(BeatmapManager.QueryBeatmap(b => b.ID == createdId).BeatmapSet.Beatmaps.Count == initialCount + 1,
					"cancel creation leaves no extra difficulty");
			}
			menuItems(createdEditor).Single(i => i.Text.Value.Equals(WebCommonStrings.ButtonsSave)).Action.Value.Invoke();
			yield return () => !hasUnsavedChanges(createdEditor);
			check(BeatmapManager.QueryBeatmap(b => b.ID == createdId).Ruleset.ShortName == "shred", mode + " saved difficulty remains native Shred");
			createdEditor.Exit();
			yield return () => ScreenStack.CurrentScreen is MainMenu;
			saved = BeatmapManager.QueryBeatmap(b => b.ID == createdId);
			Beatmap.Value = BeatmapManager.GetWorkingBeatmap(saved, refetch: true);
			Ruleset.Value = saved.Ruleset;
			ScreenStack.Push(new EditorLoader());
			yield return () => ScreenStack.CurrentScreen is Editor ready && ready.ReadyForUse;
			Editor reopened = (Editor)ScreenStack.CurrentScreen;
			ShredHitObject note = reopened.Dependencies.Get<EditorBeatmap>().HitObjects.Cast<ShredHitObject>().Single();
			check(note.Row == 1 && note.Fret == 2 && close(note.StartTime, 6500) && close(note.Duration, 750), mode + " saved LN survives reopening");
			reopened.Exit();
			yield return () => ScreenStack.CurrentScreen is MainMenu;
		}
	}

	private static bool hasUnsavedChanges(Editor editor) => (bool)typeof(Editor).GetProperty("HasUnsavedChanges", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);

	private static MenuItem createShredItem(Editor editor)
	{
		MenuItem creation = menuItems(editor).Single(i => i.Text.Value.Equals(EditorStrings.CreateNewDifficulty));
		return creation.Items.Single(i => i.Text.Value.ToString() == "shred");
	}

	private static IEnumerable<MenuItem> menuItems(Editor editor) => flattenMenu(editor.ChildrenOfType<EditorMenuBar>().Single().Items);
	private static IEnumerable<MenuItem> flattenMenu(IEnumerable<MenuItem> items)
	{
		foreach (MenuItem item in items)
		{
			yield return item;
			foreach (MenuItem child in flattenMenu(item.Items)) yield return child;
		}
	}
}
