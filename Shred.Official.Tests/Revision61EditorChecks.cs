using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Input.StateChanges;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Rulesets.Shred.UI;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Input;

internal partial class Revision54EditorRateChecks
{
    private IEnumerator<Func<bool>> runRevision61EditorSteps()
    {
        yield return () => ScreenStack?.CurrentScreen is MainMenu;
        Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(createFixture()), "v61.osz") });
        yield return () => import.IsCompleted; import.GetAwaiter().GetResult();
        var info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred");
        yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred"))?.Ruleset.ShortName == "shred";
        Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true); Ruleset.Value = info.Ruleset;
        ScreenStack.Push(new EditorLoader());
        yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse;
        var editor = (Editor)ScreenStack.CurrentScreen;
        var composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
        var map = editor.Dependencies.Get<EditorBeatmap>();
        var clock = editor.Dependencies.Get<EditorClock>();
        composer.StepRecorder.Enabled.Value = false; composer.SetSelectTool();
        foreach (var step in fractionalPaste61(editor, composer, map, clock)) yield return step;
        foreach (var step in gestures61(editor, composer, map, clock)) yield return step;
        foreach (var step in navigation61(editor, composer, map, clock)) yield return step;
        check((bool)typeof(Editor).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null)!, "v61 fixture saves");
        editor.Exit(); yield return () => ScreenStack.CurrentScreen is MainMenu;
    }

    private IEnumerable<Func<bool>> fractionalPaste61(Editor editor, ShredHitObjectComposer composer, EditorBeatmap map, EditorClock clock)
    {
        var history = (EditorChangeHandler)editor.Dependencies.Get<IEditorChangeHandler>();
        var container = (ShredBlueprintContainer)composer.BlueprintContainer;
        foreach (double offset in new[] { -.95, -.8, .8, .95 })
        foreach (bool native in new[] { true, false })
        {
            clock.Stop(); clock.Seek(1000);
            map.BeginChange(); map.Clear();
            var originals = new[] { new ShredHitObject { StartTime = 5000, Duration = 400, Fret = 1 }, new ShredHitObject { StartTime = 5000, Duration = 400, Fret = 2 } };
            map.AddRange(originals);
            map.AddRange(originals.Select(n => new ShredHitObject { StartTime = 1000 + offset, Duration = 400, Fret = n.Fret }));
            map.EndChange(); map.SelectedHitObjects.Clear(); map.SelectedHitObjects.AddRange(originals);
            yield return () => true;
            if (native)
            {
                typeof(Editor).GetMethod("Copy", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null);
                typeof(Editor).GetMethod("Paste", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null);
            }
            else { container.ClipboardCommand(Key.C); container.ClipboardCommand(Key.V); }
            yield return () => true;
            var heads = map.HitObjects.Cast<ShredHitObject>().Where(n => n.StartTime < 2000).ToArray();
            check(heads.Length == 2 && heads.All(n => n.Duration >= 399), $"pasted LN pair replaces coincident pair without short remnants ({native}/{offset}): " + string.Join(",", heads.Select(n => n.StartTime + ":" + n.Duration)));
            check(heads[0].StartTime == heads[1].StartTime && heads[0].EndTime == heads[1].EndTime, "pasted heads and LN tails have exactly equal timestamps");
            check(composer.BlueprintContainer.SelectionBlueprints.Children.All(b => map.HitObjects.Contains(b.Item)), "no stale blueprints after paste collision");
            history.RestoreState(-1); yield return () => true;
            check(map.HitObjects.Count == 4 && map.HitObjects.Cast<ShredHitObject>().All(n => n.Duration == 400), "single undo restores original LN pair");
            history.RestoreState(1); yield return () => true;
            check(map.HitObjects.Count == 4 && map.HitObjects.Cast<ShredHitObject>().All(n => n.Duration >= 399), "redo preserves only intended LN pair");
        }
        foreach (bool native in new[] { true, false })
        {
            map.BeginChange(); map.Clear();
            var pattern = Enumerable.Range(0, 32).SelectMany(i => new[] {
                new ShredHitObject { StartTime = 1000 + i * 60000d / 172 / 2, Duration = 60000d / 172 / 2, Fret = 1 },
                new ShredHitObject { StartTime = 1000 + i * 60000d / 172 / 2 + .8, Duration = 60000d / 172 / 2, Row = 1, Fret = 1 },
                new ShredHitObject { StartTime = 1000 + i * 60000d / 172 / 2 + .9, Duration = 60000d / 172 / 2, Row = 1, Fret = 2 } }).ToArray();
            map.AddRange(pattern); map.EndChange(); map.SelectedHitObjects.Clear(); map.SelectedHitObjects.AddRange(pattern);
            clock.Seek(10000); yield return () => true;
            if (native)
            {
                typeof(Editor).GetMethod("Copy", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null);
                typeof(Editor).GetMethod("Paste", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null);
            }
            else { container.ClipboardCommand(Key.C); container.ClipboardCommand(Key.V); }
            yield return () => true;
            var pasted = map.HitObjects.Cast<ShredHitObject>().Where(n => n.StartTime > 9000).ToArray();
            check(pasted.Length == 96 && pasted.All(n => n.Duration > 170), "large paste preserves every LN and its duration, native=" + native);
            check(pasted.Chunk(3).All(group => group.All(n => n.StartTime == group[0].StartTime && n.EndTime == group[0].EndTime)), "large paste aligns every three-note chord and its tails exactly, native=" + native);
            GameplayChecks.CheckPastedShred(pasted);
            check(true, "all 96 copied LN heads playable with full-chord shred, native=" + native);
        }
    }

    private IEnumerable<Func<bool>> gestures61(Editor editor, ShredHitObjectComposer composer, EditorBeatmap map, EditorClock clock)
    {
        var divisor = composer.Dependencies.Get<BindableBeatDivisor>();
        divisor.SetArbitraryDivisor(4); map.TimelineZoom = .25;
        clock.Stop(); clock.Seek(750);
        map.BeginChange(); map.Clear();
        foreach (var group in map.ControlPointInfo.Groups.ToArray()) map.ControlPointInfo.RemoveGroup(group);
        map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
        map.ControlPointInfo.Add(1200, new TimingControlPoint { BeatLength = 250 });
        var original = new[] { new ShredHitObject { StartTime = 1000, Duration = 600, Fret = 1 }, new ShredHitObject { StartTime = 1512.5, Duration = 125, Row = 1, Fret = 3 } };
        map.AddRange(original); map.EndChange(); map.SelectedHitObjects.Clear(); map.SelectedHitObjects.AddRange(original);
        yield return () => true;
        Vector2 from = composer.Grid.ToScreenSpace(composer.Grid.PositionAt(9, 1512.5));
        Vector2 to = composer.Grid.ToScreenSpace(composer.Grid.PositionAt(10, 1575));
        revision57Key(Key.ControlLeft, true);
        new MousePositionAbsoluteInput { Position = from }.Apply(pointerInput.CurrentState, pointerInput);
        yield return () => true;
        new MouseButtonInput(MouseButton.Left, true).Apply(pointerInput.CurrentState, pointerInput);
        yield return () => true;
        new MousePositionAbsoluteInput { Position = (from + to) / 2 }.Apply(pointerInput.CurrentState, pointerInput);
        yield return () => true;
        check(original[0].Duration == 600 && original[1].Duration == 125 && original.All(map.HitObjects.Contains), "Ctrl+drag does not trim or remove originals while copy is in flight");
        new MousePositionAbsoluteInput { Position = to }.Apply(pointerInput.CurrentState, pointerInput);
        yield return () => true;
        new MouseButtonInput(MouseButton.Left, false).Apply(pointerInput.CurrentState, pointerInput);
        revision57Key(Key.ControlLeft, false); yield return () => true;
        var copies = map.HitObjects.Cast<ShredHitObject>().Except(original).OrderBy(n => n.StartTime).ToArray();
        check(copies.Length == 2 && close(copies[1].StartTime, 1575) && copies[1].Column == 10,
            "grabbed middle of copied selection follows cursor across a BPM boundary: " + string.Join(",", copies.Select(n => n.StartTime + ":" + n.Column)));
        check(close(copies[0].StartTime, 1062.5) && copies[0].Column == 2 && copies[0].Duration == 600 && copies[1].Duration == 125,
            "copy drag preserves relative times, rows and LN lengths");
        check(original[0].Duration == 600 && original[1].Duration == 125, "original notes remain intact after copying to other columns");

        map.TimelineZoom = .5;
        foreach (bool timeline in new[] { false, true })
        foreach (bool reverse in new[] { false, true })
        {
            clock.Seek(timeline ? 1250 : 750);
            map.BeginChange(); map.Clear();
            var heads = new[] { 1000d, 1250, 1500 }.SelectMany(t => new[] {
                new ShredHitObject { StartTime = t, Fret = 2 },
                new ShredHitObject { StartTime = t - .95, Row = 1, Fret = 2, Duration = 100 } }).ToArray();
            map.AddRange(heads); map.EndChange(); map.SelectedHitObjects.Clear(); map.SelectedHitObjects.Add(heads[reverse ? 4 : 0]);
            yield return () => true;
            DateTime laidOut = DateTime.UtcNow.AddMilliseconds(200); yield return () => DateTime.UtcNow >= laidOut;
            var target = heads[reverse ? 0 : 4];
            Vector2 point = composer.Grid.ToScreenSpace(composer.Grid.PositionAt(target.Column, target.StartTime));
            if (timeline)
            {
                var b = editor.ChildrenOfType<TimelineHitObjectBlueprint>().Single(b => b.Item == target);
                point = b.ToScreenSpace(new Vector2(0, b.DrawHeight / 2));
            }
            revision57Key(Key.ShiftLeft, true);
            new MousePositionAbsoluteInput { Position = point }.Apply(pointerInput.CurrentState, pointerInput);
            yield return () => true; revision57Click(point); revision57Key(Key.ShiftLeft, false);
            yield return () => true;
            check(map.SelectedHitObjects.ToHashSet().SetEquals(heads), $"Shift range includes both full boundary rows ({timeline}/{reverse}): " + string.Join(",", map.SelectedHitObjects.Select(n => n.StartTime)));
        }
        foreach (int row in new[] { 0, 1 })
        {
            map.BeginChange(); map.Clear(); var ln = new ShredHitObject { StartTime = 999.1, Duration = 500, Row = row, Fret = 2 };
            map.Add(ln); map.EndChange(); clock.Seek(1000); yield return () => true;
            var drawable = editor.ChildrenOfType<DrawableShredEditorNote>().Single(n => n.HitObject == ln);
            var visual = drawable.ChildrenOfType<ShredNoteVisual>().Single();
            var head = (osu.Framework.Graphics.Drawable)typeof(ShredNoteVisual).GetField("head", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(visual)!;
            check(head.Alpha == 1 && composer.Grid.HeadContains(ln, composer.Grid.ToScreenSpace(composer.Grid.Receiver(row, 2))), "rounded LN head stays visible and selectable at judgement line, row=" + row);
            clock.Seek(1125); yield return () => true;
            check(head.Alpha == 0, "LN head disappears on next step, leaving sustain colour, row=" + row);
        }
        clock.Seek(5000); yield return () => true;
        var edge = composer.Grid.ToScreenSpace(new Vector2(composer.Grid.DrawWidth / 2, 0));
        composer.Grid.ScrollAtEdge(edge, 1, 20); double ordinary = clock.CurrentTimeAccurate - 5000;
        clock.Seek(5000); composer.Grid.ScrollAtEdge(edge, 1, 20, speed: 3);
        check(ordinary > 0 && close(clock.CurrentTimeAccurate - 5000, ordinary * 3), "marquee edge scroll is three times faster");
    }
    private IEnumerable<Func<bool>> navigation61(Editor editor, ShredHitObjectComposer composer, EditorBeatmap map, EditorClock clock)
    {
        var playback = composer.Playback; playback.ReturnOnPause.Value = true;
        foreach (bool recorder in new[] { false, true })
        foreach (bool wheel in new[] { false, true })
        {
            composer.StepRecorder.Enabled.Value = recorder;
            clock.Stop(); clock.Seek(3000); yield return () => true;
            new MousePositionAbsoluteInput { Position = composer.Grid.ScreenSpaceDrawQuad.Centre }.Apply(pointerInput.CurrentState, pointerInput);
            revision57Tap(Key.Space); yield return () => clock.IsRunning && clock.CurrentTime > 3100;
            for (int i = 0; i < 3; i++)
            {
                if (wheel) new MouseScrollRelativeInput { Delta = new Vector2(0, -1) }.Apply(pointerInput.CurrentState, pointerInput);
                else revision57Tap(Key.Right);
                double destination = clock.CurrentTimeAccurate;
                yield return () => true;
                yield return () => clock.CurrentTime > destination + 80;
                revision57Tap(Key.Space);
                yield return () => true;
                check(!clock.IsRunning && Math.Abs(clock.CurrentTimeAccurate - destination) < 1, $"return anchor follows latest {(wheel ? "wheel" : "arrow")} seek, recorder={recorder}, repeat={i}, expected={destination}, got={clock.CurrentTimeAccurate}");
                revision57Tap(Key.Space); yield return () => clock.IsRunning;
            }
            revision57Tap(Key.Space); yield return () => !clock.IsRunning;
        }
        int saved = ShredScrollSpeed.Speed;
        var settings = new ShredGameplaySettings();
        var wrapper = new global::osu.Game.Tests.Visual.DependencyProvidingContainer {
            Width = 700, Height = 200,
            CachedDependencies = new (Type, object)[] { (typeof(global::osu.Game.Overlays.OverlayColourProvider), new global::osu.Game.Overlays.OverlayColourProvider(270)) }, Child = settings };
        Add(wrapper); yield return () => settings.IsLoaded && settings.ChildrenOfType<FormSliderBar<int>>().Any(s => s.IsLoaded);
        var slider = settings.ChildrenOfType<FormSliderBar<int>>().Single();
        slider.Current.Value = 28; yield return () => true;
        check(ShredScrollSpeed.Speed == 28 && Math.Abs(ShredScrollSpeed.ApproachTime - 410.17857) < .001 && slider.LabelFormat(28).ToString().Contains("410"), "gameplay scroll slider displays travel duration and drives actual speed");
        ShredScrollSpeed.Adjust(-1); yield return () => true;
        check(slider.Current.Value == 27, "F3/F4 adjustment is reflected in settings");
        slider.Current.SetDefault(); yield return () => true;
        check(ShredScrollSpeed.Speed == 20, "scroll reset restores default speed 20");
        ShredScrollSpeed.Setting.Value = saved; Remove(wrapper, true);
    }
}
