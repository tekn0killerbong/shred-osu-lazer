using System.ComponentModel;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Input.Bindings;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Database;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Overlays.Settings.Sections.Input;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Compatibility;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.GameplayTest;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Input;
using RulesetType = osu.Game.Rulesets.Ruleset;

internal partial class Revision54EditorRateChecks
{
    private IEnumerator<Func<bool>> runRevision59Steps()
    {
        yield return () => ScreenStack?.CurrentScreen is MainMenu;
        Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(createFixture()), "v59.osz") });
        yield return () => import.IsCompleted;
        import.GetAwaiter().GetResult();
        foreach (string mode in composeOnly ? new[] { "shred" } : new[] { "osu", "taiko", "fruits", "mania", "shred" })
        {
            var info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-" + mode);
            yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-" + mode))?.Ruleset.ShortName == mode;
            Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true);
            Ruleset.Value = info.Ruleset;
            ScreenStack.Push(new EditorLoader());
            yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse && e.ChildrenOfType<PlaybackRateControl>().Any();
            Editor editor = (Editor)ScreenStack.CurrentScreen;
            EditorClock clock = editor.Dependencies.Get<EditorClock>();
            PlaybackRateControl control = editor.ChildrenOfType<PlaybackRateControl>().Single();
            RoundedSliderBar<double> slider = control.ChildrenOfType<RoundedSliderBar<double>>().Single();
            check(close(control.Current.Value, 1), mode + " session opens at 100%");
            control.Current.Value = .63;
            yield return () => true;
            check(string.IsNullOrEmpty(slider.TooltipText.ToString()) && control.ChildrenOfType<OsuSpriteText>().Single().Text.ToString() == "63%", mode + " no floating text; right indicator retained");
            foreach (EditorScreenMode tab in composeOnly ? Array.Empty<EditorScreenMode>() : new[] { EditorScreenMode.Timing, EditorScreenMode.SongSetup, EditorScreenMode.Design, EditorScreenMode.Verify, EditorScreenMode.Compose })
            {
                editor.Mode.Value = tab;
                yield return () => true;
                check(close(control.Current.Value, .63), mode + " preserves speed on " + tab);
                clock.Stop(); clock.Seek(5000);
                editor.TestGameplay();
                yield return () => ScreenStack.CurrentScreen is EditorPlayer p && p.IsLoaded;
                EditorPlayer player = (EditorPlayer)ScreenStack.CurrentScreen;
                GameplayClockContainer testClock = player.ChildrenOfType<GameplayClockContainer>().Single();
                yield return () => !testClock.IsPaused.Value;
                check(close(((IClock)testClock).Rate, .63), mode + " Test from " + tab + " uses 63%");
                player.Exit();
                yield return () => ScreenStack.CurrentScreen == editor;
            }
            if (mode == "shred")
            {
                control.Current.Value = 1;
                foreach (var step in revision59Bindings(editor)) yield return step;
                foreach (var step in revision59Playback(editor)) yield return step;
                foreach (var step in revision59Duplicates(editor)) yield return step;
                foreach (var step in emptyNavigationSteps(editor)) yield return step;
                check((bool)typeof(Editor).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null)!, "edited fixture saves before closing");
            }
            editor.Exit();
            yield return () => ScreenStack.CurrentScreen is MainMenu;
        }
    }

    private IEnumerable<Func<bool>> revision59Bindings(Editor editor)
    {
        RealmAccess realm = editor.Dependencies.Get<RealmAccess>();
        // Reproduce a pre-v59 profile, including custom bindings and a missing Right slot.
        foreach (bool custom in new[] { false, true })
        {
            realm.Write(r =>
            {
                foreach (var b in r.All<RealmKeyBinding>().Where(b => b.RulesetName == "shred" && b.Variant == RulesetType.EDITOR_VARIANT).ToArray())
                    if (b.ActionInt == 2 || b.ActionInt == 3 || b.ActionInt == 15) r.Remove(b);
                r.Add(new RealmKeyBinding(ShredEditorAction.EmptyStep, new KeyCombination(custom ? InputKey.T : InputKey.J), "shred", RulesetType.EDITOR_VARIANT));
                r.Add(new RealmKeyBinding(ShredEditorAction.EmptyStepAlternate, new KeyCombination(custom ? InputKey.U : InputKey.N), "shred", RulesetType.EDITOR_VARIANT));
                if (custom) r.Add(new RealmKeyBinding(ShredEditorAction.EmptyStepNavigation, new KeyCombination(InputKey.Y), "shred", RulesetType.EDITOR_VARIANT));
                ShredEmptyStepBindings.Apply(r);
                ShredEmptyStepBindings.Apply(r);
            });
            var slots = realm.Run(r => r.All<RealmKeyBinding>().Where(b => b.RulesetName == "shred" && b.Variant == RulesetType.EDITOR_VARIANT && b.ActionInt == 2).Detach());
            var expected = new ShredRuleset().GetDefaultKeyBindings(RulesetType.EDITOR_VARIANT).Where(b => (ShredEditorAction)b.Action == ShredEditorAction.EmptyStep).Select(b => b.KeyCombination).ToArray();
            if (custom) expected = new[] { new KeyCombination(InputKey.T), new KeyCombination(InputKey.U) };
            check(slots.Count == 2 && slots.Select(b => b.KeyCombination).SequenceEqual(expected), "two persisted Empty step slots; migration is idempotent, custom=" + custom);
            check(realm.Run(r => !r.All<RealmKeyBinding>().Any(b => b.RulesetName == "shred" && b.Variant == RulesetType.EDITOR_VARIANT && (b.ActionInt == 3 || b.ActionInt == 15))), "obsolete Empty step actions removed");
        }
        var settings = new RulesetEditorBindingsSubsection(new ShredRuleset().RulesetInfo);
        var wrapper = new global::osu.Game.Tests.Visual.DependencyProvidingContainer
        {
            Width = 700, Height = 1000,
            CachedDependencies = new (Type, object)[] { (typeof(global::osu.Game.Overlays.OverlayColourProvider), new global::osu.Game.Overlays.OverlayColourProvider(270)) },
            Child = settings
        };
        Add(wrapper);
        yield return () => settings.IsLoaded && settings.ChildrenOfType<KeyBindingRow>().Any(r => (int)r.Action == 2);
        KeyBindingRow row = settings.ChildrenOfType<KeyBindingRow>().Single(r => (int)r.Action == 2);
        yield return () => row.IsLoaded && row.KeyBindings.Count == 2;
        check(settings.ChildrenOfType<KeyBindingRow>().Count(r => (int)r.Action == 2 || (int)r.Action == 3 || (int)r.Action == 15) == 1,
            "actual settings show one Empty step row with two editable buttons");
        check(!row.IsDefault.Value, "custom keys correctly show reset");
        row.RestoreDefaults();
        yield return () => row.IsDefault.Value;
        check(row.KeyBindings.Count == 2, "actual settings reset succeeds with two slots");
        Remove(wrapper, true);
        var actions = new ShredRuleset().GetDefaultKeyBindings().Take(18).Select(b => (ShredAction)b.Action).ToArray();
        int[] ids = Enumerable.Range(100, 5).Concat(Enumerable.Range(109, 5)).Concat(Enumerable.Range(105, 4)).Concat(Enumerable.Range(114, 4)).ToArray();
        check(actions.Select(a => (int)a).SequenceEqual(ids), "binding order: both fret rows, then both pick rows; IDs preserved");
        foreach (ShredAction action in actions)
            check(typeof(ShredAction).GetField(action.ToString())!.GetCustomAttribute<DescriptionAttribute>()!.Description.StartsWith("Row "), "spaced label " + action);
        yield return () => true;
    }

    private IEnumerable<Func<bool>> revision59Playback(Editor editor)
    {
        var composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
        var clock = editor.Dependencies.Get<EditorClock>();
        var playback = composer.Playback;
        composer.StepRecorder.Enabled.Value = true;
        playback.ReturnOnPause.Value = true;
        clock.Stop(); clock.Seek(1000);
        yield return () => composer.StepRecorder.HasFocus;
        DateTime ready = DateTime.UtcNow;
        yield return () => (DateTime.UtcNow - ready).TotalSeconds > .4;
        revision57Tap(Key.Space);
        yield return () => clock.IsRunning && clock.CurrentTime > 1250;
        double stop = clock.CurrentTimeAccurate;
        revision57Tap(Key.Space);
        yield return () => !clock.IsRunning;
        check(Math.Abs(clock.CurrentTimeAccurate - 1000) < 1, "Space pause returns to playback origin");
        revision57Key(Key.ControlLeft, true); revision57Tap(Key.Space); revision57Key(Key.ControlLeft, false);
        check(clock.IsRunning && Math.Abs(clock.CurrentTimeAccurate - stop) < 1, "Ctrl+Space resumes at real stop before last return; expected=" + stop + ", actual=" + clock.CurrentTimeAccurate + ", running=" + clock.IsRunning + ", focus=" + composer.StepRecorder.HasFocus);
        yield return () => clock.CurrentTime > stop + 100;
        double stop2 = clock.CurrentTimeAccurate;
        revision57Key(Key.ControlLeft, true); revision57Tap(Key.Space); revision57Key(Key.ControlLeft, false);
        check(!clock.IsRunning && Math.Abs(clock.CurrentTimeAccurate - stop2) < 1, "Ctrl+Space pause stays at current position");
        revision57Tap(Key.Space);
        yield return () => clock.IsRunning;
        clock.Seek(4000);
        yield return () => true;
        yield return () => clock.CurrentTime > 4200;
        revision57Tap(Key.Space);
        check(!clock.IsRunning && Math.Abs(clock.CurrentTimeAccurate - 4000) < 1, "native seek while playing updates Return on pause anchor");
        clock.SeekSmoothlyTo(3000);
        revision57Tap(Key.Space);
        check(clock.IsRunning && Math.Abs(clock.CurrentTimeAccurate - 3000) < 1, "play starts at latest smooth navigation destination");
        yield return () => clock.CurrentTime > 3200;
        revision57Tap(Key.Space);
        check(Math.Abs(clock.CurrentTimeAccurate - 3000) < 1, "pause returns to latest navigation position");
        playback.ReturnOnPause.Value = false;
        double off = clock.CurrentTimeAccurate;
        revision57Key(Key.ControlLeft, true); revision57Tap(Key.Space); revision57Key(Key.ControlLeft, false);
        check(!clock.IsRunning && close(clock.CurrentTimeAccurate, off), "Ctrl+Space has no special action with Return off");
        composer.StepRecorder.Enabled.Value = false;
        foreach (int row in new[] { 0, 1 })
        foreach (int fret in Enumerable.Range(1, 5))
        {
            clock.Seek(5000);
            Vector2 centre = composer.Grid.Receiver(row, fret);
            float half = composer.Grid.NoteSize(fret).Y / 2;
            float direction = row == 0 ? 1 : -1;
            foreach (float y in new[] { -half, 0, half })
            {
                composer.Grid.ScrollAtEdge(composer.Grid.ToScreenSpace(centre + new Vector2(0, y)), row * 6 + fret, 30);
                check(close(clock.CurrentTimeAccurate, 5000), "no scroll inside case row=" + row + " fret=" + fret);
            }
            composer.Grid.ScrollAtEdge(composer.Grid.ToScreenSpace(centre + new Vector2(0, direction * (half + 20))), row * 6 + fret, 30);
            check(clock.CurrentTimeAccurate < 5000, "scroll begins downstream of case row=" + row + " fret=" + fret);
        }
    }

    private IEnumerable<Func<bool>> revision59Duplicates(Editor editor)
    {
        var composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
        var map = editor.Dependencies.Get<EditorBeatmap>();
        var clock = editor.Dependencies.Get<EditorClock>();
        var history = (EditorChangeHandler)editor.Dependencies.Get<IEditorChangeHandler>();
        composer.Playback.ReturnOnPause.Value = false;
        composer.SetSelectTool();
        foreach (int column in new[] { 1, 7, 12 })
        foreach (double offset in new[] { 0d, .2, -.2 })
        foreach (string operation in new[] { "native", "shred", "placement", "batch" })
        {
            clock.Stop(); clock.Seek(1000);
            ShredHitObject make(double time, double duration) => new ShredHitObject
            {
                StartTime = time, Duration = duration, Row = column < 12 ? column / 6 : 0, Fret = column < 12 ? column % 6 : 0,
                Articulation = column == 12 ? global::osu.Game.Rulesets.Shred.Gameplay.ShredPracticeArticulation.Wham : global::osu.Game.Rulesets.Shred.Gameplay.ShredPracticeArticulation.Pick
            };
            map.BeginChange(); map.Clear();
            var source = make(5000, 500);
            map.Add(source); map.Add(make(1000 + offset, column == 12 ? 1 : 0));
            map.EndChange();
            map.SelectedHitObjects.Clear(); map.SelectedHitObjects.Add(source);
            yield return () => true;
            if (operation == "native")
            {
                typeof(Editor).GetMethod("Copy", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null);
                typeof(Editor).GetMethod("Paste", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null);
            }
            else if (operation == "shred")
            {
                var container = (ShredBlueprintContainer)composer.BlueprintContainer;
                container.ClipboardCommand(Key.C); container.ClipboardCommand(Key.V);
            }
            else
            {
                var note = make(1000, 500);
                composer.BeginPlacementChange(note);
                map.Add(note);
                if (operation == "batch") map.Add(make(1000 + .1, column == 12 ? 1 : 0));
                composer.EndPlacementChange();
            }
            yield return () => true;
            var heads = map.HitObjects.Cast<ShredHitObject>().Where(n => n.StartTime < 2000).ToArray();
            string label = operation + " column=" + column + " offset=" + offset;
            check(heads.Length == 1 && close(heads[0].Duration, 500), label + " one head remains and LN is preserved; heads=" + string.Join(",", heads.Select(n => n.StartTime + ":" + n.Duration)));
            history.RestoreState(-1);
            yield return () => true;
            check(map.HitObjects.Count == 2, label + " undo restores original state in one action");
            history.RestoreState(1);
            yield return () => true;
            check(map.HitObjects.Count == 2 && map.HitObjects.Cast<ShredHitObject>().Where(n => n.StartTime < 2000).Single().Duration == 500, label + " redo remains duplicate-free and keeps LN");
        }
    }
}
