using osu.Framework.Allocation;
using osu.Framework.Input.StateChanges;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Scoring;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Menu;

internal partial class Revision54EditorRateChecks
{
    private IEnumerator<Func<bool>> runRevision60EditorSteps()
    {
        yield return () => ScreenStack?.CurrentScreen is MainMenu;
        Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(createFixture()), "v60.osz") });
        yield return () => import.IsCompleted;
        import.GetAwaiter().GetResult();
        var info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred");
        yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred"))?.Ruleset.ShortName == "shred";
        Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true);
        Ruleset.Value = info.Ruleset;
        ScreenStack.Push(new EditorLoader());
        yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse;
        var editor = (Editor)ScreenStack.CurrentScreen;
        editor.Mode.Value = EditorScreenMode.SongSetup;
        yield return () => editor.ChildrenOfType<ShredAccuracySlider>().Any(s => s.IsLoaded);
        var slider = editor.ChildrenOfType<ShredAccuracySlider>().Single();
        foreach (float od in new[] { 0f, 5f, 10f })
        {
            slider.Current.Value = od;
            yield return () => true;
            var attr = slider.TooltipContent;
            check(attr.Description.ToString() == ShredAccuracyDisplay.Description && attr.AdditionalMetrics.Length == 2,
                "short description and two window metrics");
            check(attr.AdditionalMetrics[0].Name.ToString() == "GREAT hit window" && attr.AdditionalMetrics[0].Value.ToString() == $"±{120 - 9 * od} ms", "GREAT value at " + od);
            check(attr.AdditionalMetrics[1].Name.ToString() == "MISS hit window" && attr.AdditionalMetrics[1].Value.ToString() == $"±{180 - 3 * od} ms", "MISS value at " + od);
            var editing = editor.Dependencies.Get<EditorBeatmap>();
            check(editing.Difficulty.OverallDifficulty == od, "slider updates editable difficulty");
            var savedInfo = editing.BeatmapInfo.Clone();
            savedInfo.Difficulty = editing.Difficulty.Clone();
            var selection = new ShredRuleset().GetBeatmapAttributesForDisplay(savedInfo,
                Array.Empty<osu.Game.Rulesets.Mods.Mod>()).First();
            check(selection.AdditionalMetrics.SequenceEqual(attr.AdditionalMetrics), "song selection and editor use same coloured metrics");
            new MousePositionAbsoluteInput { Position = slider.ScreenSpaceDrawQuad.Centre }.Apply(pointerInput.CurrentState, pointerInput);
            DateTime hovered = DateTime.UtcNow;
            yield return () => (DateTime.UtcNow - hovered).TotalSeconds > 1;
            check(this.ChildrenOfType<BeatmapAttributeTooltip>().Any(t => t.IsPresent && t.ChildrenOfType<OsuSpriteText>().Any(x => x.Text.ToString() == $"±{120 - 9 * od} ms")),
                "actual hover opens native rich tooltip at " + od);
        }
        slider.Current.Value = 5;
        yield return () => true;
        editor.Exit();
        yield return () => ScreenStack.CurrentScreen is MainMenu;
    }
}
