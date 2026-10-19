using System.Reflection;
using ManagedBass;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components.TernaryButtons;
using osu.Game.Screens.Menu;
using osu.Game.Skinning;

internal partial class Revision54EditorRateChecks
{
    private IEnumerator<Func<bool>> runRevision63AudioSteps()
    {
        yield return () => ScreenStack?.CurrentScreen is MainMenu;
        var import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(createFixture()), "v63.osz") });
        yield return () => import.IsCompleted;
        import.GetAwaiter().GetResult();
        var info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred");
        yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred"))?.Ruleset.ShortName == "shred";
        Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true);
        Ruleset.Value = info.Ruleset;
        ScreenStack.Push(new EditorLoader());
        yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse;
        var editor = (Editor)ScreenStack.CurrentScreen;
        var composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
        var map = composer.Dependencies.Get<EditorBeatmap>();
        var clock = composer.Dependencies.Get<EditorClock>();
        clock.Stop();
        composer.Playback.ReturnOnPause.Value = false;
        foreach (Action step in Revision49Checks.ImportSkin(composer.Dependencies.Get<SkinManager>(), Revision49Checks.CreateConflictingSkin()))
        {
            step();
            yield return () => true;
        }
        map.SelectedHitObjects.Clear(); map.SelectedHitObjects.AddRange(map.HitObjects);
        foreach (string bank in HitSampleInfo.ALL_BANKS)
        {
            composer.ChildrenOfType<SampleBankTernaryButton>().Single(b => b.BankName == bank).NormalButton.TriggerClick();
            composer.ChildrenOfType<SampleBankTernaryButton>().Single(b => b.BankName == bank).AdditionsButton.TriggerClick();
            foreach (var pair in new[] { (HitSampleInfo.HIT_WHISTLE, GlobalAction.EditorToggleWhistleSound),
                         (HitSampleInfo.HIT_FINISH, GlobalAction.EditorToggleFinishSound), (HitSampleInfo.HIT_CLAP, GlobalAction.EditorToggleClapSound) })
                if (composer.SelectionSampleStates[pair.Item1].Value != TernaryState.True)
                    composer.ChildrenOfType<DrawableTernaryButton<GlobalAction>>().Single(b => b.Action == pair.Item2).TriggerClick();
            clock.Seek(1800);
            yield return () => composer.ChildrenOfType<DrawableShredEditorNote>().First().ChildrenOfType<PoolableSkinnableSample>()
                .Count(s => s.Sample != null && underlying(s).IsLoaded) == 4;
            // Give the debounced editor changes a frame to propagate before querying audio.
            yield return () => true;
            var sounds = composer.ChildrenOfType<DrawableShredEditorNote>().First().ChildrenOfType<PoolableSkinnableSample>().ToArray();
            check(sounds.Length == 4, bank + " exactly four intended samples, no metadata marker sounds");
            using var resources = new osu.Game.Rulesets.Shred.ShredRuleset().CreateResourceStore();
            foreach (var sound in sounds)
            {
                var sample = underlying(sound);
                var requested = (HitSampleInfo)typeof(PoolableSkinnableSample).GetField("sampleInfo", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sound)!;
                string expectedName = "Gameplay/" + bank + "-" + requested.Name;
                check(sample.Name == expectedName && sample.IsLoaded, "native editor resolves bundled " + expectedName + " despite conflicting skin");
                check(close(sample.Length, Revision45Checks.Files.Single(f => "Gameplay/" + f.name == expectedName).ms), "decoded duration " + expectedName);
                check(close(sound.AggregateFrequency.Value, 1) && close(sound.AggregateTempo.Value, 1), "no pitch/rate alteration at 100%: " + expectedName);
                check(close(sound.AggregateVolume.Value, requested.Volume / 100.0), "no sample gain boost: " + expectedName);
                int id = (int)sample.GetType().GetProperty("SampleId")!.GetValue(sample)!;
                byte[] pcm = Revision63AudioChecks.Pcm(resources.Get("Samples/" + expectedName + ".wav"));
                byte[] decoded = new byte[pcm.Length];
                check(Bass.SampleGetData(id, decoded) && decoded.SequenceEqual(pcm), "actual editor sample waveform matches DLL PCM: " + expectedName);
            }
            clock.Start();
            yield return () => clock.CurrentTime > 2300;
            clock.Stop();
            check(sounds.All(s => s.Played), bank + " all four assigned hitsounds play when crossing the note");
        }
        // The harness closes the isolated game once all checks have passed.
        // Do not leave the editor here: that opens the unsaved-changes prompt.
    }

    private static ISample underlying(PoolableSkinnableSample sound)
        => (ISample)sound.Sample.GetType().GetField("sample", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sound.Sample)!;
}
