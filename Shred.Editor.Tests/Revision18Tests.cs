using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK.Input;

internal static partial class Program
{
    private static void RunRevision18Tests()
    {
        var cases = new (string name, Action run)[]
        {
            ("Missing picks miss only the last pressed frets in every press order", () =>
            {
                int[][] orders = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
                foreach (var order in orders)
                for (int picks = 0; picks <= 3; picks++)
                foreach (int row in new[] { 0, 1 })
                {
                    using var game = new TestSession();
                    var notes = new[] { game.Add(1000, row, 1), game.Add(1000, row, 2), game.Add(1000, row, 3) };
                    game.Seek(1000);
                    foreach (int i in order) game.Press(100 + row * 9 + i);
                    for (int i = 0; i < picks; i++) game.Press(105 + row * 9 + i);
                    game.Seek(2000);
                    foreach (var note in notes) note.CheckExpiry();
                    for (int i = 0; i < 3; i++)
                    {
                        check(notes[order[i]].HeadHit == (i < picks));
                        if (i >= picks) check(notes[order[i]].Result.Type == HitResult.Miss);
                    }
                }
            }),
            ("Chord pick order is independent on the two rows", () =>
            {
                using var game = new TestSession();
                var a = game.Add(1000, 0, 1); var b = game.Add(1000, 0, 2);
                var c = game.Add(1000, 1, 1); var d = game.Add(1000, 1, 2);
                game.Seek(1000);
                foreach (int key in new[] { 100, 101, 110, 109 }) game.Press(key);
                game.Press(105); game.Press(114);
                check(a.HeadHit && d.HeadHit && !b.HeadJudged && !c.HeadJudged);
                game.Press(106); game.Press(115);
                check(b.HeadHit && c.HeadHit);
            }),
            ("Partial sustain chord keeps paid holds and misses unpaid heads", () =>
            {
                using var game = new TestSession();
                var first = game.Add(1000, 0, 4, ShredPracticeArticulation.Pick, 2000);
                var last = game.Add(1000, 0, 2, ShredPracticeArticulation.Pick, 2000);
                game.Seek(1000); game.Press(103); game.Press(101); game.Press(105);
                check(first.IsHolding && !last.HeadJudged);
                game.Seek(2000); first.CheckExpiry(); last.CheckExpiry();
                check(first.IsHolding && last.HeadJudged && !last.HeadHit);
            }),
            ("An incomplete chord cannot leave the HP chain armed after its miss", () =>
            {
                using var game = new TestSession();
                var a = game.Add(1000, 0, 1); var b = game.Add(1000, 0, 2);
                var hp = game.Add(2000, 0, 3, ShredPracticeArticulation.HammerPull);
                game.Seek(1000); game.Press(100); game.Press(101); game.Press(105); check(a.HeadHit);
                game.Seek(2000); game.Press(102);
                check(b.Result.Type == HitResult.Miss && !hp.HeadJudged);
            }),
            ("Native Ctrl Q toggles a disabled recorder with either Control key", () =>
            {
                foreach (Key control in new[] { Key.ControlLeft, Key.ControlRight })
                {
                    using var input = new LiveRecorderCommands();
                    input.Press(control); input.Press(Key.Q); check(input.Recorder.Enabled.Value);
                    input.Press(Key.Q, true); check(input.Recorder.Enabled.Value);
                    input.Release(Key.Q); input.Press(Key.Q); check(!input.Recorder.Enabled.Value);
                    input.Release(Key.Q); input.Release(control);
                    input.Press(Key.Q); check(!input.Recorder.Enabled.Value);
                }
            }),
            ("Custom recorder toggle replaces Ctrl Q and remains available disabled", () =>
            {
                using var input = new LiveRecorderCommands(new[] { new KeyBinding(InputKey.F8, ShredEditorAction.ToggleStepRecorder) });
                input.Press(Key.F8); check(input.Recorder.Enabled.Value);
                input.Release(Key.F8); input.Press(Key.F8); check(!input.Recorder.Enabled.Value);
                input.Press(Key.ControlLeft); input.Press(Key.Q); check(!input.Recorder.Enabled.Value);
            }),
            ("Grid spacing bindings work disabled in opposite directions without wrapping", () =>
            {
                using var input = new LiveRecorderCommands();
                input.Divisor.Value = 4;
                input.Press(Key.I); equal(8, input.Divisor.Value); input.Release(Key.I);
                input.Press(Key.O); equal(4, input.Divisor.Value); input.Release(Key.O);
                input.Divisor.Value = 1; input.Press(Key.O); equal(1, input.Divisor.Value); input.Release(Key.O);
                input.Divisor.Value = 16; input.Press(Key.I); equal(16, input.Divisor.Value); input.Release(Key.I);
                input.Press(Key.ControlLeft); input.Press(Key.I); equal(16, input.Divisor.Value);
            }),
            ("Grid types cycle in both directions and retain a custom preset", () =>
            {
                using var input = new LiveRecorderCommands();
                input.Divisor.ValidDivisors.Value = BeatDivisorPresetCollection.Custom(5);
                input.Recorder.ApplyGridCommand(ShredEditorAction.IncreaseGridType);
                check(input.Divisor.ValidDivisors.Value.Type == BeatDivisorType.Common);
                input.Recorder.ApplyGridCommand(ShredEditorAction.DecreaseGridType);
                check(input.Divisor.ValidDivisors.Value.Type == BeatDivisorType.Custom);
                input.Recorder.ApplyGridCommand(ShredEditorAction.DecreaseGridType);
                check(input.Divisor.ValidDivisors.Value.Type == BeatDivisorType.Triplets);
                input.Recorder.ApplyGridCommand(ShredEditorAction.IncreaseGridType);
                check(input.Divisor.ValidDivisors.Value.Presets.SequenceEqual(new[] { 1, 5 }));
            }),
            ("Coarser grid dragging preserves a short LN head offset and duration", () =>
            {
                double start = 1000 + 500.0 / 3, end = start + 125;
                double head = ShredEditorOperations.DragTime(start, 0, 500, t => Math.Round(t / 500) * 500);
                double duration = ShredEditorOperations.DragDuration(start, end);
                equal(125, duration); equal(start + 500, head); equal(end + 500, head + duration);
                equal(duration, ShredEditorOperations.DragDuration(head, head + duration));
            }),
            ("Undo restores deleted selection and redo clears it across multiple edits", () =>
            {
                var ruleset = new ShredRuleset();
                using var store = new TestRulesetStore(ruleset.RulesetInfo);
                osu.Game.Beatmaps.Formats.Decoder.RegisterDependencies(store);
                var map = new Beatmap<ShredHitObject>(); map.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
                map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                map.HitObjects.AddRange(new[] { new ShredHitObject { StartTime = 1000, Fret = 1 }, new ShredHitObject { StartTime = 2000, Fret = 3 }, new ShredHitObject { StartTime = 3000, Fret = 5 } });
                foreach (var note in map.HitObjects) note.ApplyDefaults(map.ControlPointInfo, map.Difficulty);
                using var beatmap = new EditorBeatmap(map);
                using var handler = new BeatmapEditorChangeHandler(beatmap);
                beatmap.SelectedHitObjects.AddRange(beatmap.HitObjects.Take(2));
                beatmap.RemoveRange(beatmap.SelectedHitObjects.ToArray());
                equal(1, beatmap.HitObjects.Count); equal(0, beatmap.SelectedHitObjects.Count);
                handler.RestoreState(-1);
                equal(3, beatmap.HitObjects.Count); equal(2, beatmap.SelectedHitObjects.Count);
                check(beatmap.SelectedHitObjects.Select(n => n.StartTime).SequenceEqual(new[] { 1000d, 2000d }));
                handler.RestoreState(1); equal(1, beatmap.HitObjects.Count); equal(0, beatmap.SelectedHitObjects.Count);
                handler.RestoreState(-1);
                beatmap.SelectedHitObjects.Clear(); beatmap.SelectedHitObjects.Add(beatmap.HitObjects.Last());
                beatmap.RemoveRange(beatmap.SelectedHitObjects.ToArray());
                handler.RestoreState(-1);
                equal(1, beatmap.SelectedHitObjects.Count); equal(3000, beatmap.SelectedHitObjects[0].StartTime);
                handler.RestoreState(1); equal(2, beatmap.HitObjects.Count); equal(0, beatmap.SelectedHitObjects.Count);
                check(beatmap.HitObjects.All(n => n.StartTime != 3000));
                handler.RestoreState(-1); equal(1, beatmap.SelectedHitObjects.Count); equal(3000, beatmap.SelectedHitObjects[0].StartTime);
            })
        };
        foreach (var item in cases) { item.run(); Console.WriteLine("PASS " + item.name); }
    }

    private partial class LiveRecorderCommands : ShredRecorderCommandContainer
    {
        private readonly InputState state = new InputState();
        public readonly ShredStepRecorder Recorder = new ShredStepRecorder(new ShredRuleset().RulesetInfo);
        public readonly BindableBeatDivisor Divisor = new BindableBeatDivisor(4);
        public LiveRecorderCommands(IKeyBinding[] custom = null) : base(new ShredRuleset().RulesetInfo)
        {
            KeyBindings = custom ?? DefaultKeyBindings.ToArray();
            Add(Recorder);
            typeof(Drawable).GetProperty("Parent").SetValue(Recorder, this);
            typeof(ShredStepRecorder).GetProperty("divisor", hidden).SetValue(Recorder, Divisor);
        }
        protected override IEnumerable<Drawable> KeyBindingInputQueue => new Drawable[] { Recorder };
        public void Press(Key key, bool repeat = false)
        {
            state.Keyboard.Keys.SetPressed(key, true);
            Handle(new KeyDownEvent(state, key, repeat));
        }
        public void Release(Key key)
        {
            state.Keyboard.Keys.SetPressed(key, false);
            Handle(new KeyUpEvent(state, key));
        }
    }
}
