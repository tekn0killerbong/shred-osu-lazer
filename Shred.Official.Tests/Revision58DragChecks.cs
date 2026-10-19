using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Input;
using osu.Framework.Input.StateChanges;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Input;

internal partial class Revision54EditorRateChecks
{
    private IEnumerable<Func<bool>> tailDragSteps(Editor editor)
    {
        ShredHitObjectComposer composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
        EditorBeatmap map = editor.Dependencies.Get<EditorBeatmap>();
        EditorClock clock = editor.Dependencies.Get<EditorClock>();
        EditorChangeHandler history = (EditorChangeHandler)editor.Dependencies.Get<IEditorChangeHandler>();
        composer.StepRecorder.Enabled.Value = false;
        ((IFocusManager)typeof(osu.Framework.Graphics.Drawable)
            .GetMethod("GetContainingFocusManager", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(composer.StepRecorder, null)!).ChangeFocus(null);
        composer.SetSelectTool();
        map.TimelineZoom = 0.25;
        composer.Dependencies.Get<BindableBeatDivisor>().SetArbitraryDivisor(4);
        DateTime inputReady = DateTime.UtcNow.AddSeconds(1);
        yield return () => DateTime.UtcNow >= inputReady;

        foreach (int column in new[] { 1, 7, 12 })
        foreach (double at in new[] { 1500d, 2000d, 2250d })
        foreach (double duration in new[] { 0d, 250d })
        foreach (double from in new[] { 2500d, 500d })
        {
            double viewTime = from == 500 ? 250 : 750;
            clock.Stop(); clock.Seek(viewTime);
            map.BeginChange();
            foreach (var note in map.HitObjects.ToArray()) map.Remove(note);
            foreach (ControlPointGroup group in map.ControlPointInfo.Groups.ToArray()) map.ControlPointInfo.RemoveGroup(group);
            map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            ShredHitObject noteAt(double time, double length, int lane) => new ShredHitObject
            {
                StartTime = time, Duration = length, Row = lane < 12 ? lane / 6 : 0, Fret = lane < 12 ? lane % 6 : 0,
                Articulation = lane == 12 ? ShredPracticeArticulation.Wham : ShredPracticeArticulation.Pick
            };
            int partnerColumn = column == 1 ? 9 : 3;
            ShredHitObject tail = noteAt(1000, 1000, column);
            ShredHitObject neighbour = noteAt(1000, 2000, 5);
            ShredHitObject source = noteAt(from, duration, column);
            ShredHitObject partnerTail = noteAt(1000, 1000, partnerColumn);
            ShredHitObject partner = noteAt(from, duration, partnerColumn);
            foreach (ShredHitObject note in new[] { tail, neighbour, source }) map.Add(note);
            if (column != 12) { map.Add(partnerTail); map.Add(partner); }
            map.EndChange();
            map.SelectedHitObjects.Clear(); map.SelectedHitObjects.Add(source);
            if (column != 12) map.SelectedHitObjects.Add(partner);
            yield return () => composer.BlueprintContainer.SelectionBlueprints.Children.OfType<ShredSelectionBlueprint>()
                .Any(b => b.HitObject == source && b.DrawableObject != null && b.IsPresent);
            Vector2 screenAt(double time) => composer.Grid.ToScreenSpace(composer.Grid.PositionAt(column, time));
            void move(double time) => new MousePositionAbsoluteInput { Position = screenAt(time) }.Apply(pointerInput.CurrentState, pointerInput);
            move(from);
            yield return () => true;
            check(composer.BlueprintContainer.SelectionBlueprints.Children.OfType<ShredSelectionBlueprint>()
                .Any(b => b.HitObject == source && b.IsHovered), "drag starts on the actual hovered note blueprint; screen=" + screenAt(from)
                + "; hits=" + string.Join(",", composer.BlueprintContainer.SelectionBlueprints.Children.OfType<ShredSelectionBlueprint>()
                    .Where(b => b.HitObject == source).Select(b => $"hit={b.ReceivePositionalInputAt(screenAt(from))},alive={b.IsAlive},present={b.IsPresent}")));
            check(composer.Grid.ContainsScreen(screenAt(from)) && !composer.Grid.EndHandleContains(source, screenAt(from)),
                "drag fixture head is visible and is not an LN resize handle");
            new MouseButtonInput(MouseButton.Left, true).Apply(pointerInput.CurrentState, pointerInput);
            yield return () => true;
            move(from + (1500 - from) / 2);
            yield return () => true;
            move(1500);
            yield return () => true;
            check(tail.EndTime == 2000, "passing across an LN body does not trim before drop");
            move(at);
            yield return () => true;
            new MouseButtonInput(MouseButton.Left, false).Apply(pointerInput.CurrentState, pointerInput);
            yield return () => true;
            string label = $"drag column={column}, from={from}, to={at}, duration={duration}";
            double expectedEnd = Math.Min(at, 2000);
            int expectedCount = column == 12 ? 3 : 5;
            check(map.HitObjects.Contains(tail) && tail.EndTime == expectedEnd,
                label + " keeps the original LN and cuts only at the dropped head; objects=" + string.Join(";", map.HitObjects.Cast<ShredHitObject>().Select(n => $"{n.Column}:{n.StartTime}-{n.EndTime}")));
            check(map.HitObjects.Count == expectedCount && source.StartTime == at && source.Duration == duration && neighbour.EndTime == 3000,
                label + " preserves moved length and unrelated lanes");
            if (column != 12)
                check(partner.StartTime == at && partner.Duration == duration && partnerTail.EndTime == expectedEnd,
                    label + " applies the same cut to every moved chord member");
            check(close(clock.CurrentTimeAccurate, viewTime), label + " drop keeps grid position");
            history.RestoreState(-1);
            yield return () => true;
            bool lane(ShredHitObject note) => column == 12 ? note.IsWham : !note.IsWham && note.Column == column;
            check(map.HitObjects.Count == expectedCount && map.HitObjects.Cast<ShredHitObject>().Single(n => lane(n) && n.StartTime == 1000).EndTime == 2000
                && map.HitObjects.Cast<ShredHitObject>().Single(n => lane(n) && n.StartTime == from).Duration == duration,
                label + " one undo restores both original LN and moved note");
            check(map.SelectedHitObjects.Count == (column == 12 ? 1 : 2) && map.SelectedHitObjects.All(n => n.StartTime == from)
                && close(clock.CurrentTimeAccurate, viewTime), label + " undo keeps selection and grid");
            history.RestoreState(1);
            yield return () => true;
            check(map.HitObjects.Count == expectedCount && map.HitObjects.Cast<ShredHitObject>().Single(n => lane(n) && n.StartTime == 1000).EndTime == expectedEnd
                && map.HitObjects.Cast<ShredHitObject>().Single(n => lane(n) && n.StartTime == at).Duration == duration,
                label + " redo restores exact cut and move");
            check(map.SelectedHitObjects.Count == (column == 12 ? 1 : 2) && map.SelectedHitObjects.All(n => n.StartTime == at)
                && close(clock.CurrentTimeAccurate, viewTime), label + " redo keeps selection and grid");
        }
    }
}
