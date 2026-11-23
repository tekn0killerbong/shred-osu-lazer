using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Input.StateChanges;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Timing;
using osu.Game.Screens.Edit.Timing.Multiselect;
using osu.Game.Screens.Menu;
using osuTK;
using osuTK.Input;

internal partial class Revision54EditorRateChecks
{
    private IEnumerator<Func<bool>> runRevision65EditorSteps()
    {
        yield return () => ScreenStack?.CurrentScreen is MainMenu;
        Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(createFixture()), "v65.osz") });
        yield return () => import.IsCompleted;
        import.GetAwaiter().GetResult();
        var info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred");
        yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred"))?.Ruleset.ShortName == "shred";
        Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true);
        Ruleset.Value = info.Ruleset;
        ScreenStack.Push(new EditorLoader());
        yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse;
        var editor = (Editor)ScreenStack.CurrentScreen;
        foreach (var step in timing65(editor)) yield return step;
        foreach (var step in marquee65(editor)) yield return step;
        check((bool)typeof(Editor).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null)!, "v65 fixture saves");
    }

    private IEnumerable<Func<bool>> timing65(Editor editor)
    {
        var map = editor.Dependencies.Get<EditorBeatmap>();
        var clock = editor.Dependencies.Get<EditorClock>();
        clock.Stop(); clock.Seek(0);
        map.BeginChange(); map.Clear();
        foreach (var group in map.ControlPointInfo.Groups.ToArray()) map.ControlPointInfo.RemoveGroup(group);
        foreach (var pair in new[] { (0d, 500d), (4000d, 400d), (8000d, 600d) })
            map.ControlPointInfo.GroupAt(pair.Item1, true).Add(new TimingControlPoint { BeatLength = pair.Item2 });
        var notes = new[] { 1000d, 4000, 4100, 4200, 8000, 8150, 8300 }.Select((t, i) => new ShredHitObject {
            StartTime = t, Fret = 1 + i % 5, Duration = i == 2 ? 200 : 0
        }).ToArray();
        map.AddRange(notes); map.EndChange();
        yield return () => true;
        editor.Mode.Value = EditorScreenMode.Timing;
        MultiTimingContent content = null;
        yield return () => (content = editor.ChildrenOfType<MultiTimingContent>().SingleOrDefault())?.IsLoaded == true;
        var selection = content.Selection;
        content.MultipleSettings.MoveObjects.Current.Value = true;
        selection.Click(map.ControlPointInfo.GroupAt(4000), false, false);
        yield return () => content.SingleSettings.ChildrenOfType<TapTimingControl>().Any(t => t.IsLoaded);
        var offset = content.SingleSettings.ChildrenOfType<FormDiscreteAdjustmentControl<double>>().Single(c => c.Caption.ToString() == "Offset");
        foreach (double delta in new[] { 1d, -1, 37, -37 })
        {
            var before = notes.Select(n => (n.StartTime, n.Duration)).ToArray();
            double original = selection.Groups.Single().Time;
            offset.Current.Value = original + delta;
            yield return () => true;
            check(close(selection.Groups.Single().Time, original + delta), "single timing offset changes");
            for (int i = 0; i < notes.Length; i++)
                check(close(notes[i].StartTime, before[i].StartTime + (i >= 1 && i <= 3 ? delta : 0)) && close(notes[i].Duration, before[i].Duration),
                    $"single offset {delta}: section and LN move once #{i}, actual={notes[i].StartTime}");
        }
        selection.Click(map.ControlPointInfo.GroupAt(8000), true, false);
        yield return () => true;
        foreach (double delta in new[] { .25, -.25, 100d, -100 })
        {
            var before = notes.Select(n => (n.StartTime, n.Duration)).ToArray();
            content.MultipleSettings.Offset.Current.Value = selection.Groups[0].Time + delta;
            yield return () => true;
            for (int i = 0; i < notes.Length; i++)
                check(close(notes[i].StartTime, before[i].StartTime + (i > 0 ? delta : 0)) && close(notes[i].Duration, before[i].Duration),
                    $"multi offset {delta}: adjacent different BPM sections move once #{i}, actual={notes[i].StartTime}");
        }
        content.MultipleSettings.MoveObjects.Current.Value = true;
        content.MultipleSettings.Offset.Current.Value += .25;
        yield return () => true;
        selection.Click(selection.Groups[0], false, false);
        yield return () => true;
        double fractionalOrigin = selection.Groups.Single().Time;
        double fractionalNote = notes[2].StartTime;
        offset.Current.Value += 1;
        check(close(notes[2].StartTime - fractionalNote, selection.Groups.Single().Time - fractionalOrigin),
            $"single offset after fractional multi-edit keeps exact relative timing: object delta={notes[2].StartTime - fractionalNote}, point delta={selection.Groups.Single().Time - fractionalOrigin}");
        yield return () => true;
        check(close(notes[2].StartTime - fractionalNote, selection.Groups.Single().Time - fractionalOrigin), "fractional correction survives object processing");
        content.MultipleSettings.MoveObjects.Current.Value = false;
        double[] fixedTimes = notes.Select(n => n.StartTime).ToArray();
        offset.Current.Value += 1;
        yield return () => true;
        check(notes.Select(n => n.StartTime).SequenceEqual(fixedTimes), "disabled movement keeps objects stationary after processing");
        editor.Mode.Value = EditorScreenMode.Compose;
        yield return () => true;
    }

    private IEnumerable<Func<bool>> marquee65(Editor editor)
    {
        var composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
        var map = editor.Dependencies.Get<EditorBeatmap>();
        var clock = editor.Dependencies.Get<EditorClock>();
        composer.StepRecorder.Enabled.Value = false;
        clock.Stop(); clock.Seek(300); map.TimelineZoom = .2;
        map.BeginChange(); map.Clear();
        var notes = new[] { 1000d, 1500, 2000, 2500 }.SelectMany(t => Enumerable.Range(0, 2).SelectMany(row => Enumerable.Range(1, 5)
            .Select(f => new ShredHitObject { StartTime = t, Row = row, Fret = f }))).ToArray();
        map.AddRange(notes); map.EndChange(); map.SelectedHitObjects.Clear();
        yield return () => true;
        DateTime layout = DateTime.UtcNow.AddMilliseconds(200); yield return () => DateTime.UtcNow >= layout;
        composer.SetSelectTool(); yield return () => true;
        check(composer.BlueprintContainer.CurrentTool is osu.Game.Rulesets.Edit.Tools.SelectTool, "marquee fixture uses Select after map initialization");
        void mouse(Vector2 point) => new MousePositionAbsoluteInput { Position = composer.Grid.ToScreenSpace(point) }.Apply(pointerInput.CurrentState, pointerInput);
        void button(MouseButton b, bool down) => new MouseButtonInput(b, down).Apply(pointerInput.CurrentState, pointerInput);
        void expect(IEnumerable<ShredHitObject> expected, string label) => check(map.SelectedHitObjects.ToHashSet().SetEquals(expected),
            label + "; selected=" + string.Join(",", map.SelectedHitObjects.Cast<ShredHitObject>().Select(n => n.StartTime + ":" + n.Column)));
        foreach (int row in new[] { 0, 1 })
        foreach (bool backwards in new[] { false, true })
        foreach (bool control in new[] { false, true })
        {
            map.SelectedHitObjects.Clear(); clock.Seek(300); yield return () => true;
            double start = backwards ? 3000 : 500, end = backwards ? 500 : 3000;
            Vector2 from = composer.Grid.PositionAt(row * 6 + 2, start);
            Vector2 to = new Vector2(composer.Grid.PositionAt(row * 6 + 3, start).X, composer.Grid.PositionAt(row * 6 + 3, end).Y);
            check(composer.Grid.ContainsScreen(composer.Grid.ToScreenSpace(from)), "gesture starts inside grid");
            if (control) revision57Key(Key.ControlLeft, true);
            mouse(from); yield return () => true; button(MouseButton.Left, true); yield return () => true;
            mouse((from + to) / 2); yield return () => true; mouse(to); yield return () => true;
            expect(notes.Where(n => n.Row == row && n.Fret >= 2 && n.Fret <= 3), $"row {row} backward={backwards}, Ctrl={control}: fixed origin columns follow skewed lanes");
            button(MouseButton.Right, true); yield return () => true;
            expect(notes.Where(n => n.Fret >= 2 && n.Fret <= 3), "right held includes corresponding columns and times in both grids");
            check(!editor.ChildrenOfType<osu.Game.Graphics.UserInterface.OsuContextMenu>().Any(m => m.State == MenuState.Open), "right held during selection never opens a menu");
            button(MouseButton.Right, false); yield return () => true;
            expect(notes.Where(n => n.Row == row && n.Fret >= 2 && n.Fret <= 3), "right release restores origin grid selection while drag continues");
            button(MouseButton.Right, true); yield return () => true;
            button(MouseButton.Left, false); yield return () => true;
            button(MouseButton.Right, false); revision57Key(Key.ControlLeft, false); yield return () => true;
            expect(notes.Where(n => n.Fret >= 2 && n.Fret <= 3), "ending drag while right held keeps both grids selected");
            check(map.HitObjects.Count == notes.Length && !editor.ChildrenOfType<osu.Game.Graphics.UserInterface.OsuContextMenu>().Any(m => m.State == MenuState.Open), "ending two-button drag never deletes or opens menu");
        }
        map.SelectedHitObjects.Clear(); clock.Seek(300); yield return () => true;
        Vector2 origin = composer.Grid.PositionAt(2, 500);
        Vector2 edge = new Vector2(composer.Grid.PositionAt(3, 500).X, 5);
        mouse(origin); yield return () => true; button(MouseButton.Left, true); yield return () => true;
        mouse(edge); yield return () => true; button(MouseButton.Right, true);
        DateTime scrollUntil = DateTime.UtcNow.AddMilliseconds(650); yield return () => DateTime.UtcNow >= scrollUntil;
        check(clock.CurrentTimeAccurate > 300, "marquee auto-scroll advances the grid");
        expect(notes.Where(n => n.Fret >= 2 && n.Fret <= 3), "auto-scroll retains the original columns and offscreen notes in both grids");
        button(MouseButton.Left, false); button(MouseButton.Right, false); yield return () => true;
    }
}
