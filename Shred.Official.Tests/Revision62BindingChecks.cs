using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Input.Bindings;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings.Sections.Input;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Compatibility;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Input;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Screens.Menu;
using osuTK.Input;
using RulesetType = osu.Game.Rulesets.Ruleset;

internal partial class Revision54EditorRateChecks
{
    private IEnumerator<Func<bool>> runRevision62EditorSteps()
    {
        yield return () => ScreenStack?.CurrentScreen is MainMenu;
        migration62();
        Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(createFixture()), "v62.osz") });
        yield return () => import.IsCompleted;
        import.GetAwaiter().GetResult();
        var info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred");
        yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred"))?.Ruleset.ShortName == "shred";
        Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true);
        Ruleset.Value = info.Ruleset;
        ScreenStack.Push(new EditorLoader());
        yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse;
        var editor = (Editor)ScreenStack.CurrentScreen;
        foreach (var step in revision59Bindings(editor)) yield return step;

        var ruleset = new ShredRuleset();
        var settings = new ShredEditorSettings(ruleset);
        var bindings = new RulesetEditorBindingsSubsection(ruleset.RulesetInfo);
        var wrapper = new global::osu.Game.Tests.Visual.DependencyProvidingContainer
        {
            Width = 800, Height = 1800,
            CachedDependencies = new (Type, object)[] { (typeof(OverlayColourProvider), new OverlayColourProvider(270)) },
            Children = new osu.Framework.Graphics.Drawable[] { settings, bindings }
        };
        Add(wrapper);
        yield return () => settings.IsLoaded && bindings.IsLoaded && bindings.ChildrenOfType<KeyBindingRow>().Count() == 8;
        check(!settings.ChildrenOfType<KeyBindingRow>().Any(), "Gameplay/Shred contains no duplicate editor bindings");
        var rows = bindings.ChildrenOfType<KeyBindingRow>().ToArray();
        var order = new[] { ShredEditorAction.Pick, ShredEditorAction.HammerPull, ShredEditorAction.ToggleStepRecorder,
            ShredEditorAction.EmptyStep, ShredEditorAction.DecreaseGridSpacing, ShredEditorAction.IncreaseGridSpacing,
            ShredEditorAction.DecreaseGridType, ShredEditorAction.IncreaseGridType };
        check(rows.Select(row => (ShredEditorAction)row.Action).SequenceEqual(order), "native Editor/Shred row order");
        foreach (var row in rows)
        {
            int count = 2;
            if ((ShredEditorAction)row.Action is ShredEditorAction.Pick or ShredEditorAction.HammerPull or ShredEditorAction.ToggleStepRecorder) count = 1;
            check(row.KeyBindings.Count == count && row.Defaults.Count() == count, "matching editable and default slot counts: " + row.Action);
            check(row.IsDefault.Value, "fresh/reset row has no reset arrow: " + row.Action);
        }
        check(rows[0].ChildrenOfType<FormFieldCaption>().Single().Caption.ToString() == "Type pick", "visible Type pick label");
        check(rows[1].ChildrenOfType<FormFieldCaption>().Single().Caption.ToString() == "Type hammer pull", "visible Type hammer pull label");
        check(rows[4].ChildrenOfType<FormFieldCaption>().Single().Caption.ToString() == "Decrease grid spacing", "visible 7 label: Decrease grid spacing");
        check(rows[5].ChildrenOfType<FormFieldCaption>().Single().Caption.ToString() == "Increase grid spacing", "visible 8 label: Increase grid spacing");
        foreach (var row in rows.Skip(4))
        {
            check(row.KeyBindings[1].KeyCombination.Equals(new KeyCombination(InputKey.None)), "blank secondary: " + row.Action);
            row.KeyBindings[1].KeyCombination = new KeyCombination(InputKey.F8);
            row.RestoreDefaults();
        }
        yield return () => rows.All(row => row.IsDefault.Value);
        check(rows.Skip(4).All(row => row.KeyBindings[1].KeyCombination.Equals(new KeyCombination(InputKey.None))), "grid row reset restores empty secondary without exception");
        Remove(wrapper, true);

        var composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
        var clock = editor.Dependencies.Get<EditorClock>();
        var divisor = composer.Dependencies.Get<BindableBeatDivisor>();
        clock.Stop();
        foreach (bool enabled in new[] { false, true })
        {
            composer.StepRecorder.Enabled.Value = enabled;
            if (enabled) yield return () => composer.StepRecorder.HasFocus;
            divisor.ValidDivisors.Value = BeatDivisorPresetCollection.COMMON;
            divisor.SetArbitraryDivisor(4);
            revision57Tap(Key.Number7);
            check(divisor.Value == 2, "7 decreases spacing with recorder=" + enabled + "; divisor=" + divisor.Value);
            revision57Tap(Key.Number8);
            check(divisor.Value == 4, "8 increases spacing with recorder=" + enabled);
            revision57Tap(Key.Number0);
            check(divisor.ValidDivisors.Value.Type == BeatDivisorType.Triplets, "0 increases type to triplets with recorder=" + enabled);
            revision57Tap(Key.Number9);
            check(divisor.ValidDivisors.Value.Type == BeatDivisorType.Common, "9 decreases type to common with recorder=" + enabled);
            foreach (var key in new[] { Key.I, Key.O, Key.P, Key.Q, Key.W, Key.E, Key.R, Key.BracketLeft }) revision57Tap(key);
            check(divisor.Value == 4 && divisor.ValidDivisors.Value.Type == BeatDivisorType.Common, "former bare grid keys no longer change grid");
        }
        composer.StepRecorder.Enabled.Value = false;
        revision57Key(Key.ControlLeft, true); revision57Tap(Key.Q); revision57Key(Key.ControlLeft, false);
        yield return () => composer.StepRecorder.Enabled.Value && composer.StepRecorder.HasFocus;
        check(true, "Ctrl+Q still enables recorder");
        revision57Key(Key.ControlLeft, true); revision57Tap(Key.Q); revision57Key(Key.ControlLeft, false);
        check(!composer.StepRecorder.Enabled.Value, "Ctrl+Q still disables recorder");
        foreach (var step in emptyNavigationSteps(editor)) yield return step;
        check((bool)typeof(Editor).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null)!, "v62 fixture saves");
        editor.Exit();
        yield return () => ScreenStack.CurrentScreen is MainMenu;
    }

    private void migration62()
    {
        var ruleset = new ShredRuleset();
        var defaults = ruleset.GetDefaultKeyBindings(RulesetType.EDITOR_VARIANT).ToArray();
        var oldGrid = new Dictionary<ShredEditorAction, InputKey[]>
        {
            [ShredEditorAction.DecreaseGridSpacing] = new[] { InputKey.I, InputKey.Q },
            [ShredEditorAction.IncreaseGridSpacing] = new[] { InputKey.O, InputKey.W },
            [ShredEditorAction.DecreaseGridType] = new[] { ShredDefaultKeys.ModeIncrease, InputKey.E },
            [ShredEditorAction.IncreaseGridType] = new[] { InputKey.P, InputKey.R }
        };
        foreach (bool registered in new[] { false, true })
        foreach (bool custom in new[] { false, true })
        {
            using var storage = new TemporaryNativeStorage("shred-v62-bindings");
            using var realm = new RealmAccess(storage, "bindings.realm");
            using (var writer = new StreamWriter(storage.GetStream("shred-migrations.ini", FileAccess.Write, FileMode.Create)))
                writer.Write("GridCommandsV45 = True\nSoundShortcutsV46 = True\nSecondaryTypesV46Corrected = True\n");
            ShredGridBindingMigration.Apply(realm, storage);
            check(realm.Run(r => !r.All<RealmKeyBinding>().Any()), "empty profile does not prematurely migrate");
            realm.Write(r =>
            {
                foreach (var group in defaults.GroupBy(b => (ShredEditorAction)b.Action))
                {
                    var keys = group.Select(b => b.KeyCombination).ToArray();
                    if (oldGrid.TryGetValue(group.Key, out var old)) keys = old.Select(k => new KeyCombination(k)).ToArray();
                    if (group.Key == ShredEditorAction.EmptyStep) keys = new[] {
                        new KeyCombination(ShredDefaultKeys.EmptyStep), new KeyCombination(ShredDefaultKeys.EmptyStepAlternate), new KeyCombination(InputKey.Right) };
                    if (custom && group.Key == ShredEditorAction.EmptyStep) keys[1] = new KeyCombination(InputKey.F8);
                    if (custom && group.Key == ShredEditorAction.DecreaseGridSpacing)
                        keys = new[] { new KeyCombination(InputKey.F6), new KeyCombination(InputKey.F7) };
                    foreach (var key in keys) r.Add(new RealmKeyBinding(group.Key, key, "shred", RulesetType.EDITOR_VARIANT));
                }
                r.Add(new RealmKeyBinding(ShredAction.Row1Fret1, new KeyCombination(InputKey.A), "shred", 0));
                r.Add(new RealmKeyBinding(ShredEditorAction.DecreaseGridSpacing, new KeyCombination(InputKey.I), "mania", RulesetType.EDITOR_VARIANT));
            });
            if (registered)
            {
                using var container = new GlobalActionContainer(null);
                new RealmKeyBindingStore(realm, null).Register(container, new[] { ruleset.RulesetInfo });
            }
            ShredGridBindingMigration.Apply(realm, storage);
            ShredGridBindingMigration.Apply(realm, storage);
            realm.Run(r =>
            {
                var actual = r.All<RealmKeyBinding>().Where(b => b.RulesetName == "shred" && b.Variant == RulesetType.EDITOR_VARIANT).ToArray();
                foreach (var group in defaults.GroupBy(b => (ShredEditorAction)b.Action))
                {
                    var expected = group.Select(b => b.KeyCombination).ToArray();
                    if (custom && group.Key == ShredEditorAction.EmptyStep) expected[1] = new KeyCombination(InputKey.F8);
                    if (custom && group.Key == ShredEditorAction.DecreaseGridSpacing)
                        expected = new[] { new KeyCombination(InputKey.F6), new KeyCombination(InputKey.F7) };
                    check(actual.Where(b => b.ActionInt == (int)group.Key).Select(b => b.KeyCombination).SequenceEqual(expected),
                        $"upgrade {group.Key}; registered={registered}, custom={custom}");
                }
                check(r.All<RealmKeyBinding>().Single(b => b.RulesetName == "mania").KeyCombination.Equals(new KeyCombination(InputKey.I)), "other rulesets unchanged");
                check(r.All<RealmKeyBinding>().Any(b => b.RulesetName == "shred" && b.Variant == 0 && b.ActionInt == (int)ShredAction.Row1Fret1
                    && b.KeyCombinationString == new KeyCombination(InputKey.A).ToString()), "gameplay bindings unchanged");
            });
            realm.Write(r => r.All<RealmKeyBinding>().Where(b => b.RulesetName == "shred" && b.Variant == RulesetType.EDITOR_VARIANT
                && b.ActionInt == (int)ShredEditorAction.DecreaseGridSpacing).ToArray()[1].KeyCombination = new KeyCombination(InputKey.Q));
            ShredGridBindingMigration.Apply(realm, storage);
            realm.Run(r => check(r.All<RealmKeyBinding>().Any(b => b.RulesetName == "shred" && b.Variant == RulesetType.EDITOR_VARIANT
                && b.ActionInt == (int)ShredEditorAction.DecreaseGridSpacing && b.KeyCombinationString == new KeyCombination(InputKey.Q).ToString()), "later custom key survives subsequent migration calls"));
        }
    }
}
