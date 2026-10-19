using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Input.StateChanges;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Database;
using osu.Game.Rulesets.Edit.Tools;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Screens.Edit.GameplayTest;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Input;

internal partial class Revision54EditorRateChecks
{
    private IEnumerator<Func<bool>> runRevision64EditorSteps()
    {
        yield return () => ScreenStack?.CurrentScreen is MainMenu;
        Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(createFixture()), "v64.osz") });
        yield return () => import.IsCompleted;
        import.GetAwaiter().GetResult();
        // Visit Shred first, then other modes: rate/pitch must not leak between editors.
        foreach (string mode in new[] { "shred", "osu", "taiko", "fruits", "mania" })
        {
            var info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-" + mode);
            yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-" + mode))?.Ruleset.ShortName == mode;
            Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true);
            Ruleset.Value = info.Ruleset;
            ScreenStack.Push(new EditorLoader());
            yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse;
            var editor = (Editor)ScreenStack.CurrentScreen;
            yield return () => true;
            var clock = editor.Dependencies.Get<EditorClock>();
            clock.Stop();
            var nativeTabs = editor.ChildrenOfType<PlaybackControl.PlaybackTabControl>().Single();
            if (mode == "shred")
            {
                var slider = editor.ChildrenOfType<PlaybackRateControl>().Single();
                var pitch = editor.ChildrenOfType<PlaybackPitchControl>().Single();
                check(!nativeTabs.IsPresent && slider.IsPresent && pitch.IsPresent, "Shred retains its slider and pitch control");
                check(slider.Current.Value == 1, "Shred speed defaults to 100 percent");
                slider.Current.Value = .65;
                pitch.Pitched.Value = true;
                clock.Seek(5000);
                editor.TestGameplay();
                yield return () => ScreenStack.CurrentScreen is EditorPlayer p && p.IsLoaded;
                var player = (EditorPlayer)ScreenStack.CurrentScreen;
                var gameplayClock = player.ChildrenOfType<GameplayClockContainer>().Single();
                yield return () => !gameplayClock.IsPaused.Value && gameplayClock.CurrentTime > 5100;
                checkClock(gameplayClock, .65, "Shred Test retains custom rate", true);
                player.Exit();
                yield return () => ScreenStack.CurrentScreen == editor;
                checkClock(gameplayClock, 1, "Shred Test releases rate on exit");
                foreach (var step in gestures64(editor)) yield return step;
                check((bool)typeof(Editor).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null)!, "v64 fixture saves");
            }
            else
            {
                check(!editor.ChildrenOfType<PlaybackRateControl>().Any(), mode + " has no Shred speed slider");
                check(!editor.ChildrenOfType<PlaybackPitchControl>().Any(), mode + " has no Shred pitch header");
                check(nativeTabs.IsPresent, mode + " keeps native playback tabs");
                nativeTabs.Current.Value = .5;
                clock.Seek(5000);
                editor.TestGameplay();
                yield return () => ScreenStack.CurrentScreen is EditorPlayer p && p.IsLoaded;
                var player = (EditorPlayer)ScreenStack.CurrentScreen;
                var gameplayClock = player.ChildrenOfType<GameplayClockContainer>().Single();
                yield return () => !gameplayClock.IsPaused.Value && gameplayClock.CurrentTime > 5100;
                checkClock(gameplayClock, 1, mode + " Test uses native speed");
                player.Exit();
                yield return () => ScreenStack.CurrentScreen == editor;
            }
            editor.Exit();
            yield return () => ScreenStack.CurrentScreen is MainMenu;
        }
    }

    private IEnumerable<Func<bool>> gestures64(Editor editor)
    {
        var composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
        var map = editor.Dependencies.Get<EditorBeatmap>();
        var clock = editor.Dependencies.Get<EditorClock>();
        composer.StepRecorder.Enabled.Value = false;
        clock.Stop(); clock.Seek(750); map.TimelineZoom = .4;
        map.BeginChange(); map.Clear();
        var notes = new[] { 1000d, 1250, 1500, 1750, 2000 }.SelectMany(t => new[] {
            new ShredHitObject { StartTime = t, Fret = 2 },
            new ShredHitObject { StartTime = t, Fret = 3 },
            new ShredHitObject { StartTime = t, Row = 1, Fret = 2 }
        }).ToArray();
        map.AddRange(notes); map.EndChange(); map.SelectedHitObjects.Clear();
        yield return () => true;
        DateTime layout = DateTime.UtcNow.AddMilliseconds(200); yield return () => DateTime.UtcNow >= layout;
        Vector2 point(ShredHitObject n) => composer.Grid.ToScreenSpace(composer.Grid.PositionAt(n.Column, n.StartTime));
        void expect(IEnumerable<ShredHitObject> expected, string label)
            => check(map.SelectedHitObjects.ToHashSet().SetEquals(expected), label + "; selected=" + string.Join(",", map.SelectedHitObjects.Cast<ShredHitObject>().Select(n => n.StartTime + ":" + n.Column)));

        revision57Tap(Key.Number2); yield return () => true;
        check(composer.BlueprintContainer.CurrentTool is ShredCompositionTool, "Pick tool starts active");
        revision57Key(Key.ControlLeft, true);
        revision57Click(point(notes[0])); revision57Key(Key.ControlLeft, false); yield return () => true;
        check(composer.BlueprintContainer.CurrentTool is SelectTool, "Ctrl click switches Pick to Select");
        expect(new[] { notes[0] }, "Ctrl click selects note without placement");
        check(map.HitObjects.Count == notes.Length, "modifier click creates no note");
        revision57Key(Key.ControlLeft, true); revision57Click(point(notes[6])); revision57Key(Key.ControlLeft, false); yield return () => true;
        expect(new[] { notes[0], notes[6] }, "Ctrl click adds a separate group");
        revision57Key(Key.ShiftLeft, true); revision57Click(point(notes[12])); revision57Key(Key.ShiftLeft, false); yield return () => true;
        expect(new[] { notes[0] }.Concat(notes.Skip(6)), "Shift starts at last Ctrl group, includes whole chords at both bounds");
        revision57Key(Key.ControlLeft, true); revision57Click(point(notes[0])); revision57Key(Key.ControlLeft, false); yield return () => true;
        expect(notes.Skip(6), "Ctrl click toggles a selected note off");

        map.SelectedHitObjects.Clear(); yield return () => true;
        revision57Tap(Key.Number3); yield return () => true;
        // Start in empty space around a pair of notes and drag across their centres.
        IEnumerable<Func<bool>> zone(int a, int b, Key modifier)
        {
            Vector2 p = point(notes[a]), q = point(notes[b]);
            Vector2 min = Vector2.ComponentMin(p, q) - new Vector2(12, 8);
            Vector2 max = Vector2.ComponentMax(p, q) + new Vector2(12, 8);
            revision57Key(modifier, true);
            foreach (var step in drag58(min, max)) yield return step;
            revision57Key(modifier, false); yield return () => true;
        }
        foreach (var step in zone(0, 1, Key.ControlLeft)) yield return step;
        check(composer.BlueprintContainer.CurrentTool is SelectTool, "Ctrl rectangle switches Hammer-pull to Select");
        expect(notes.Take(2), "Ctrl rectangle selects first group");
        foreach (var step in zone(6, 7, Key.ControlLeft)) yield return step;
        expect(notes.Take(2).Concat(notes.Skip(6).Take(2)), "second Ctrl rectangle preserves separate groups and gap");
        foreach (var step in zone(12, 13, Key.ShiftLeft)) yield return step;
        expect(notes.Take(2).Concat(notes.Skip(6)), "Shift rectangle fills range from last group, including other fret row");
        check(map.HitObjects.Count == notes.Length && notes.All(map.HitObjects.Contains), "rectangles never duplicate or replace objects");
        foreach (var step in zone(0, 1, Key.ShiftLeft)) yield return step;
        expect(notes, "backward Shift rectangle extends range without losing earlier groups");

        var before = notes.Select(n => (n.StartTime, n.Column, n.Duration)).ToArray();
        revision57Key(Key.ControlLeft, true);
        foreach (var step in drag58(point(notes[0]), point(notes[3]))) yield return step;
        revision57Key(Key.ControlLeft, false); yield return () => true;
        check(map.HitObjects.Count == notes.Length && before.SequenceEqual(notes.Select(n => (n.StartTime, n.Column, n.Duration))), "Ctrl drag starting on an object selects, never moves or duplicates");

        // The upper timeline shares the last-group anchor with the playfield.
        map.SelectedHitObjects.Clear(); clock.Seek(1500); yield return () => true;
        DateTime timelineLayout = DateTime.UtcNow.AddMilliseconds(200); yield return () => DateTime.UtcNow >= timelineLayout;
        Vector2 timelinePoint(ShredHitObject n)
        {
            var blueprint = editor.ChildrenOfType<TimelineHitObjectBlueprint>().Single(b => b.Item == n);
            return blueprint.ToScreenSpace(new Vector2(0, blueprint.DrawHeight / 2));
        }
        revision57Tap(Key.Number2); yield return () => true;
        revision57Key(Key.ControlLeft, true); revision57Click(timelinePoint(notes[0])); revision57Key(Key.ControlLeft, false); yield return () => true;
        check(composer.BlueprintContainer.CurrentTool is SelectTool, "timeline Ctrl click switches to Select");
        check(map.SelectedHitObjects.Count == 1 && map.SelectedHitObjects[0].StartTime == 1000, "timeline Ctrl click selects one note at clicked chord");
        var timelineFirst = (ShredHitObject)map.SelectedHitObjects.Single();
        revision57Key(Key.ControlLeft, true); revision57Click(timelinePoint(notes[6])); revision57Key(Key.ControlLeft, false); yield return () => true;
        check(map.SelectedHitObjects.Count == 2 && map.SelectedHitObjects.Contains(timelineFirst) && map.SelectedHitObjects.Any(n => n.StartTime == 1500), "timeline Ctrl click preserves separate groups");
        revision57Key(Key.ShiftLeft, true); revision57Click(timelinePoint(notes[12])); revision57Key(Key.ShiftLeft, false); yield return () => true;
        expect(new[] { timelineFirst }.Concat(notes.Skip(6)), "timeline Shift range uses last group and complete boundary chords");
        map.SelectedHitObjects.Clear(); yield return () => true;
        revision57Key(Key.ControlLeft, true);
        Vector2 tp = timelinePoint(notes[3]);
        foreach (var step in drag58(tp - new Vector2(2, 8), tp + new Vector2(2, 8))) yield return step;
        revision57Key(Key.ControlLeft, false); yield return () => true;
        expect(notes.Skip(3).Take(3), "timeline Ctrl rectangle selects complete row without moving notes");
        revision57Key(Key.ShiftLeft, true);
        tp = timelinePoint(notes[9]);
        foreach (var step in drag58(tp - new Vector2(2, 8), tp + new Vector2(2, 8))) yield return step;
        revision57Key(Key.ShiftLeft, false); yield return () => true;
        expect(notes.Skip(3).Take(9), "timeline Shift rectangle fills the range between groups");
        clock.Seek(750); yield return () => true;
        revision57Key(Key.ShiftLeft, true); revision57Click(point(notes[0])); revision57Key(Key.ShiftLeft, false); yield return () => true;
        expect(notes.Take(12), "range anchor is shared between timeline and playfield");
        check(before.SequenceEqual(notes.Select(n => (n.StartTime, n.Column, n.Duration))), "timeline modifiers do not move objects");

        map.SelectedHitObjects.Clear(); yield return () => true;
        revision57Tap(Key.Number3); yield return () => true;
        revision57Key(Key.ShiftLeft, true); revision57Click(point(notes[3])); revision57Key(Key.ShiftLeft, false); yield return () => true;
        check(composer.BlueprintContainer.CurrentTool is SelectTool, "Shift click switches Hammer-pull to Select");
        expect(notes.Skip(3).Take(3), "first Shift click selects whole temporal row");
        map.SelectedHitObjects.Clear(); yield return () => true;
        revision57Click(point(notes[0])); yield return () => true;
        foreach (var step in drag58(point(notes[0]), composer.Grid.ToScreenSpace(composer.Grid.PositionAt(4, notes[0].StartTime)))) yield return step;
        check(notes[0].Column != 2 && map.HitObjects.Count == notes.Length, "ordinary drag still moves existing note");

        composer.SetSelectTool(); map.SelectedHitObjects.Clear(); yield return () => true;
        new MousePositionAbsoluteInput { Position = point(notes[6]) }.Apply(pointerInput.CurrentState, pointerInput);
        yield return () => true;
        new MouseButtonInput(MouseButton.Right, true).Apply(pointerInput.CurrentState, pointerInput);
        new MouseButtonInput(MouseButton.Right, false).Apply(pointerInput.CurrentState, pointerInput);
        yield return () => true;
        check(map.HitObjects.Count == notes.Length && map.SelectedHitObjects.Contains(notes[6]), "Select right click selects object without deleting");
        check(editor.ChildrenOfType<Menu>().Any(m => m.State == MenuState.Open), "Select right click opens context menu");
        revision57Tap(Key.Escape); yield return () => true;
        revision57Tap(Key.Number2); yield return () => true;
        new MousePositionAbsoluteInput { Position = point(notes[9]) }.Apply(pointerInput.CurrentState, pointerInput);
        yield return () => true;
        double time = clock.CurrentTimeAccurate;
        new MouseButtonInput(MouseButton.Right, true).Apply(pointerInput.CurrentState, pointerInput);
        new MouseButtonInput(MouseButton.Right, false).Apply(pointerInput.CurrentState, pointerInput);
        yield return () => true;
        check(!map.HitObjects.Contains(notes[9]) && map.HitObjects.Count == notes.Length - 1, "creation-tool right click deletes object");
        check(close(clock.CurrentTimeAccurate, time), "right deletion does not move grid");
    }
}
