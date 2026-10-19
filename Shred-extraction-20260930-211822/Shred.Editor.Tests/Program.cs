using System;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osuTK.Input;
using System.IO;
using System.Text;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Mods;
using osu.Game.Rulesets.Shred.Beatmaps;
using osu.Game.Rulesets.Shred.Input;
using osu.Game.Rulesets.Shred.Replays;
using osu.Game.Utils;
using osu.Framework.IO.Stores;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Objects.Drawables;
using osu.Game.Rulesets.Shred.UI;

internal static partial class Program
{
	private sealed class TestRulesetStore : osu.Game.Rulesets.RulesetStore
    {
        private readonly RulesetInfo info;
        public TestRulesetStore(RulesetInfo info) : base(null)
        {
            this.info = info;
        }
        public override IEnumerable<RulesetInfo> AvailableRulesets => new[]
        {
            info,
            new RulesetInfo { ShortName = "mania", Name = "Legacy base", OnlineID = 3, Available = true, InstantiationInfo = info.InstantiationInfo }
        };
    }
    private static void equal(double expected, double actual)
	{
		if (Math.Abs(expected - actual) > 0.000001) throw new Exception($"Expected {expected}, got {actual}");
	}
	private static void check(bool value) { if (!value) throw new Exception("Assertion failed"); }
    private partial class PickListener : Drawable, IKeyBindingHandler<ShredAction>
    {
        public readonly HashSet<ShredAction> Held = new HashSet<ShredAction>();
        public bool OnPressed(KeyBindingPressEvent<ShredAction> e) { Held.Add(e.Action); return true; }
        public void OnReleased(KeyBindingReleaseEvent<ShredAction> e) { Held.Remove(e.Action); }
    }
    private partial class LiveBindings : ShredInputManager.ShredKeyBindingContainer
    {
        public readonly PickListener Listener = new PickListener();
        private readonly InputState state = new InputState();
        public LiveBindings() : base(new ShredRuleset().RulesetInfo, 0, SimultaneousBindingMode.Unique)
        {
            KeyBindings = new ShredRuleset().GetDefaultKeyBindings().ToArray();
            Add(Listener);
            // No graphical load in this harness: attach the listener for native release routing.
            typeof(Drawable).GetProperty("Parent").SetValue(Listener, this);
        }
        protected override IEnumerable<Drawable> KeyBindingInputQueue => new[] { Listener };
        public void Press(Key key)
        {
            state.Keyboard.Keys.SetPressed(key, true);
            Handle(new KeyDownEvent(state, key, false));
        }
        public void Release(Key key)
        {
            state.Keyboard.Keys.SetPressed(key, false);
            Handle(new KeyUpEvent(state, key));
        }
    }

	private static void Main()
	{
		var cases = new (string name, Action run)[]
		{
			("Real key bindings keep all four right-hand picks pressed independently", () =>
            {
                using var bindings = new LiveBindings();
                foreach (Key fret in new[] { Key.A, Key.S, Key.D, Key.F, Key.G, Key.Z, Key.X, Key.C, Key.V, Key.B }) bindings.Press(fret);
                Key[] keys = { Key.L, Key.Semicolon, Key.Period, Key.Slash };
                ShredAction[] actions = { ShredAction.Row1Pick3, ShredAction.Row1Pick4, ShredAction.Row2Pick3, ShredAction.Row2Pick4 };
                for (int i = 0; i < keys.Length; i++)
                {
                    bindings.Press(keys[i]);
                    for (int j = 0; j <= i; j++) check(bindings.Listener.Held.Contains(actions[j]));
                }
                bindings.Press(Key.ShiftRight);
                foreach (var action in actions) check(bindings.Listener.Held.Contains(action));
                for (int i = 0; i < keys.Length; i++)
                {
                    bindings.Release(keys[i]);
                    check(!bindings.Listener.Held.Contains(actions[i]));
                    for (int j = i + 1; j < keys.Length; j++) check(bindings.Listener.Held.Contains(actions[j]));
                }
            }),
            ("Reversed frets allow lower extras but block higher extras for notes and chords", () =>
            {
                int f(int n) => ShredChordRules.MaskFor(n);
                check(ShredChordRules.IsExact(f(3) | f(4) | f(5), f(3), 0));
                check(!ShredChordRules.IsExact(f(2) | f(3), f(3), 0));
                check(ShredChordRules.IsExact(f(2) | f(4) | f(5), f(2) | f(4), 0));
                check(!ShredChordRules.IsExact(f(2) | f(3) | f(4), f(2) | f(4), 0));
                check(!ShredChordRules.IsExact(f(5), 0, 0));
                check(ShredChordRules.IsHammerPull(f(5), f(3) | f(5), f(3), true));
                check(ShredChordRules.IsHammerPull(f(1) | f(3), f(3), f(3), false));
                check(!ShredChordRules.IsHammerPull(f(3) | f(5), f(3), f(3), false));
            }),
            ("Guitar Hero keeps fret and pick sections side by side at scaled normal centres", () =>
            {
                var skin = new osu.Game.Rulesets.Shred.Skinning.ShredSkinSettings();
                var size = new osuTK.Vector2(1000, 720);
                var fret = ShredPlayfieldGeometry.GuitarHeroReceiver(skin, size, 1.69f, 0);
                var pick = ShredPlayfieldGeometry.GuitarHeroReceiver(skin, size, 1.69f, 7);
                check(pick.X > fret.X);
                equal(fret.Y, pick.Y);
                double centreDistance = skin.FretCentre(5) + skin.ColumnWidth[4] / 2 + skin.SectionGap + 72 + 97.5 - skin.OpenCentre;
                check(Math.Abs(centreDistance * 1.69 - (pick.X - fret.X)) < 0.001);
            }),
            ("Accuracy tooltip MISS uses the gameplay ignore boundary without Outside", () =>
            {
                var info = new BeatmapInfo();
                info.Difficulty.OverallDifficulty = 5;
                var windows = new osu.Game.Rulesets.Shred.Scoring.ShredHitWindows();
                windows.SetDifficulty(5);
                string value = new ShredRuleset().GetBeatmapAttributesForDisplay(info, Array.Empty<Mod>()).First().AdditionalMetrics[1].Value.ToString();
                check(value == $"±{windows.WindowFor(osu.Game.Rulesets.Scoring.HitResult.Miss):0.##} ms");
                check(!ShredInputJudgement.IsTooEarlyToTarget(windows, -windows.WindowFor(osu.Game.Rulesets.Scoring.HitResult.Miss)));
                check(ShredInputJudgement.IsTooEarlyToTarget(windows, -windows.WindowFor(osu.Game.Rulesets.Scoring.HitResult.Miss) - 0.01));
            }),
            ("Editor accepts the autoplay replay supplied by the native wrapper", () =>
            {
                var ruleset = new ShredRuleset();
                var map = new Beatmap<ShredHitObject>();
                map.HitObjects.Add(new ShredHitObject { Fret = 2, StartTime = 500 });
                var autoplay = new ShredModAutoplay();
                using var editor = new DrawableShredEditorRuleset(ruleset, map, new Mod[] { autoplay });
                var score = autoplay.CreateScoreFromReplayData(map, new Mod[] { autoplay });
                editor.SetReplayScore(score);
                editor.SetReplayScore(autoplay.CreateScoreFromReplayData(map, new Mod[] { autoplay }));
                check(!editor.HasReplayLoaded.Value);
                equal(500, map.HitObjects[0].StartTime);
            }),
            ("Native save and reopen preserve chords, rows and triplet positions", () =>
            {
                var ruleset = new ShredRuleset();
                var map = new Beatmap<ShredHitObject>();
                map.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
                map.Difficulty.CircleSize = 12;
                map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                map.HitObjects.Add(new ShredHitObject { Row = 1, Fret = 5, StartTime = 500.0 / 3, Duration = 500 });
                map.HitObjects.Add(new ShredHitObject { Row = 0, Fret = 2, StartTime = 500.0 / 3, Articulation = ShredPracticeArticulation.HammerPull });
                map.HitObjects.Add(new ShredHitObject { Row = 0, Fret = 0, StartTime = 1000.0 / 3, Duration = 250, Articulation = ShredPracticeArticulation.Wham });
                foreach (var note in map.HitObjects) note.ApplyDefaults(map.ControlPointInfo, map.Difficulty);
                using var writer = new StringWriter();
                new LegacyBeatmapEncoder(map, null, null).Encode(writer);
                using var store = new TestRulesetStore(ruleset.RulesetInfo);
                osu.Game.Beatmaps.Formats.Decoder.RegisterDependencies(store);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(writer.ToString()));
                using var reader = new LineBufferedReader(stream);
                var decoded = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
                equal(map.HitObjects.Count, decoded.HitObjects.Count);
                for (int i = 0; i < map.HitObjects.Count; i++)
                {
                    var original = map.HitObjects[i];
                    var reopened = (ShredHitObject)decoded.HitObjects[i];
                    equal(original.Column, reopened.Column);
                    equal(original.Duration, reopened.Duration);
                    check(original.Articulation == reopened.Articulation);
                    check(Math.Abs(original.StartTime - reopened.StartTime) < 1);
                }
            }),
            ("Dragging triplets on a quarter grid preserves their spacing and reverses exactly", () =>
            {
                Func<double, double> snap = t => Math.Round(t / 125) * 125;
                double[] times = { 1000, 1000 + 500.0 / 3, 1000 + 1000.0 / 3 };
                foreach (double time in times)
                {
                    equal(time, ShredEditorOperations.DragTime(time, 1042, 1042, snap));
                    double moved = ShredEditorOperations.DragTime(time, 1042, 1417, snap);
                    equal(time + 375, moved);
                    equal(time, ShredEditorOperations.DragTime(moved, 1417, 1042, snap));
                }
            }),
            ("All forty scroll speeds have the requested traversal duration", () =>
            {
                int saved = ShredScrollSpeed.Speed;
                try
                {
                    ShredScrollSpeed.Adjust(-40);
                    for (int speed = 1; speed <= 40; speed++)
                    {
                        equal(speed, ShredScrollSpeed.Speed);
                        equal(11485.0 / speed, ShredScrollSpeed.ApproachTime);
                        ShredScrollSpeed.Adjust(1);
                    }
                }
                finally { ShredScrollSpeed.Adjust(saved - ShredScrollSpeed.Speed); }
            }),
            ("Invert type changes each selected fret independently", () =>
			{
				var notes = new[] { new ShredHitObject { Fret = 1 }, new ShredHitObject { Fret = 2, Articulation = ShredPracticeArticulation.HammerPull } };
				ShredEditorOperations.InvertTypes(notes, n => { });
				check(notes[0].Articulation == ShredPracticeArticulation.HammerPull && notes[1].Articulation == ShredPracticeArticulation.Pick);
				ShredEditorOperations.InvertTypes(notes, n => { });
				check(notes[0].Articulation == ShredPracticeArticulation.Pick);
			}),
			("Drop collisions allow touching tails but replace overlapping notes", () =>
			{
				var sustain = new ShredHitObject { Fret = 1, StartTime = 100, Duration = 200 };
				check(!ShredEditorOperations.Conflicts(sustain, new ShredHitObject { Fret = 1, StartTime = 300 }));
				check(ShredEditorOperations.Conflicts(sustain, new ShredHitObject { Fret = 1, StartTime = 200 }));
				check(!ShredEditorOperations.Conflicts(sustain, new ShredHitObject { Fret = 1, Row = 1, StartTime = 200 }));
			}),
			("All pick states can stay lit together and release independently", () =>
			{
				var rows = new[] { new ShredStringState(), new ShredStringState() };
				foreach (var row in rows) for (int i = 0; i < 4; i++) row.SetPick(i, true);
				foreach (var row in rows) for (int i = 0; i < 4; i++) check(row.IsPickHeld(i));
				rows[0].SetPick(3, false); check(rows[0].IsPickHeld(2) && rows[1].IsPickHeld(2) && rows[1].IsPickHeld(3));
			}),
			("Native mods expose rates, categories and incompatible combinations", () =>
			{
				var ruleset = new ShredRuleset();
				check(ruleset.GetModsFor(ModType.Conversion).Any(m => m is ShredModGuitarHero));
				check(ruleset.GetModsFor(ModType.Conversion).Any(m => m is ShredModMirror));
				check(ruleset.GetModsFor(ModType.Automation).Any(m => m is ShredModAutoplay));
				equal(0.75, new ShredModHalfTime().SpeedChange.Value); equal(1.5, new ShredModDoubleTime().SpeedChange.Value);
				check(!ModUtils.CheckCompatibleSet(new Mod[] { new ShredModHalfTime(), new ShredModDoubleTime() }));
				check(!ModUtils.CheckCompatibleSet(new Mod[] { new ShredModNoFail(), new ShredModSuddenDeath() }));
			}),
			("Guitar Hero converts both rows and avoids simultaneous same-fret notes", () =>
			{
				var map = new Beatmap<ShredHitObject>();
				map.HitObjects.Add(new ShredHitObject { Row = 0, Fret = 2, StartTime = 100 });
				map.HitObjects.Add(new ShredHitObject { Row = 1, Fret = 2, StartTime = 100 });
				map.HitObjects.Add(new ShredHitObject { Row = 0, Fret = 2, StartTime = 200 });
				new ShredModGuitarHero().ApplyToBeatmap(map);
				check(map.HitObjects.All(n => n.Row == 1));
				check(map.HitObjects[0].Fret != map.HitObjects[1].Fret);
				equal(1, Math.Abs(map.HitObjects[0].Fret - map.HitObjects[1].Fret));
			}),
			("Gameplay conversion mods cannot rearrange the editable source map", () =>
            {
                var map = new Beatmap<ShredHitObject>();
                map.HitObjects.Add(new ShredHitObject { Row = 0, Fret = 2, StartTime = 500.0 / 3, Duration = 250 });
                map.HitObjects.Add(new ShredHitObject { Row = 1, Fret = 2, StartTime = 500.0 / 3 });
                var before = map.HitObjects.Select(n => (n.StartTime, n.Duration, n.Row, n.Fret, n.Articulation)).ToArray();
                var hero = new ShredBeatmapConverter(map, new ShredRuleset()).Convert(CancellationToken.None);
                new ShredModGuitarHero().ApplyToBeatmap(hero);
                var mirror = new ShredModMirror();
                var mirrored = new ShredBeatmapConverter(map, new ShredRuleset()).Convert(CancellationToken.None);
                foreach (var note in mirrored.HitObjects) mirror.ApplyToHitObject(note);
                check(before.SequenceEqual(map.HitObjects.Select(n => (n.StartTime, n.Duration, n.Row, n.Fret, n.Articulation))));
                var reopened = new ShredBeatmapConverter(map, new ShredRuleset()).Convert(CancellationToken.None);
                check(before.SequenceEqual(reopened.HitObjects.Cast<ShredHitObject>().Select(n => (n.StartTime, n.Duration, n.Row, n.Fret, n.Articulation))));
            }),
            ("Conversion for retry returns fresh independent notes", () =>
			{
				var map = new Beatmap<ShredHitObject>();
				map.HitObjects.Add(new ShredHitObject { Row = 1, Fret = 3, StartTime = 500.0 / 3, Duration = 200 });
				var first = new ShredBeatmapConverter(map, new ShredRuleset()).Convert(CancellationToken.None);
				var second = new ShredBeatmapConverter(map, new ShredRuleset()).Convert(CancellationToken.None);
				check(!ReferenceEquals(first.HitObjects[0], second.HitObjects[0]));
				equal(500.0 / 3, second.HitObjects[0].StartTime);
			}),
			("AutoPlay emits fret, pick, hold and final release frames", () =>
			{
				var map = new Beatmap<ShredHitObject>();
				map.HitObjects.Add(new ShredHitObject { Row = 1, Fret = 3, StartTime = 100, Duration = 500 });
				var frames = new ShredAutoGenerator(map).Generate().Frames.OfType<ShredReplayFrame>().ToArray();
				check(frames.Any(f => f.Actions.Contains(ShredAction.Row2Fret3) && f.Actions.Contains(ShredAction.Row2Pick1)));
				check(!frames.Last().Actions.Any());
			}),
			("Song selection only exposes Accuracy and HP with timing metrics", () =>
			{
				var attributes = new ShredRuleset().GetBeatmapAttributesForDisplay(new BeatmapInfo(), Array.Empty<Mod>()).ToArray();
				check(attributes.Select(a => a.Acronym).SequenceEqual(new[] { "OD", "HP" }));
				equal(2, attributes[0].AdditionalMetrics.Length);
			}),
			("Guitar Hero icon font loads its embedded crossed-guitar glyph", () =>
			{
				using var store = new GlyphStore(new ResourceStore<byte[]>(new DllResourceStore(typeof(ShredRuleset).Assembly)), "Resources/Fonts/ShredMods");
				store.LoadFontAsync().GetAwaiter().GetResult();
				check(store.HasGlyph('\uE001'));
			}),

			("Range selection survives reordering the underlying blueprint collection", () =>
			{
				var live = new List<int> { 0, 1, 2, 3, 4 };
				var selected = new List<int>();
				ShredEditorOperations.SelectRange(live, n => n, 1, 3, n =>
				{
					live.Remove(n); live.Add(n); selected.Add(n);
				});
				check(selected.SequenceEqual(new[] { 1, 2, 3 }));
			}),
			("Horizontal and vertical native flips preserve timing and sustain length", () =>
			{
				var note = new ShredHitObject { Row = 0, Fret = 1, StartTime = 500, Duration = 750 };
				ShredEditorOperations.Flip(new[] { note }, true, true, n => { });
				equal(5, note.Fret); equal(0, note.Row);
				ShredEditorOperations.Flip(new[] { note }, false, true, n => { });
				equal(1, note.Row); equal(500, note.StartTime); equal(750, note.Duration);
				ShredEditorOperations.Flip(new[] { note }, true, true, n => { });
				ShredEditorOperations.Flip(new[] { note }, false, true, n => { });
				equal(1, note.Fret); equal(0, note.Row);
			}),
			("Mixed selection flips OPEN vertically and leaves wham unchanged", () =>
			{
				var open = new ShredHitObject { Row = 0, Fret = 0 };
				var wham = new ShredHitObject { Articulation = ShredPracticeArticulation.Wham, Duration = 125 };
				ShredEditorOperations.Flip(new[] { open, wham }, true, true, n => { });
				equal(0, open.Fret);
				ShredEditorOperations.Flip(new[] { open, wham }, false, true, n => { });
				equal(1, open.Row); equal(0, wham.Row); check(wham.IsWham);
			}),
			("Mixed conversion affects all eligible frets without rejecting the selection", () =>
			{
				var notes = new[] {
					new ShredHitObject { Fret = 1, Duration = 500 },
					new ShredHitObject { Row = 1, Fret = 5 },
					new ShredHitObject { Fret = 0 },
					new ShredHitObject { Articulation = ShredPracticeArticulation.Wham, Duration = 125 }
				};
				ShredEditorOperations.ConvertType(notes, ShredPracticeArticulation.HammerPull, n => { });
				check(notes[0].Articulation == ShredPracticeArticulation.HammerPull && notes[1].Articulation == ShredPracticeArticulation.HammerPull);
				check(notes[2].Articulation == ShredPracticeArticulation.Pick && notes[3].IsWham);
				ShredEditorOperations.ConvertType(notes, ShredPracticeArticulation.Pick, n => { });
				check(notes[0].Articulation == ShredPracticeArticulation.Pick); equal(500, notes[0].Duration);
			}),
			("Next grid tick is strict, including off-grid ends and tempo changes", () =>
			{
				var timing = new ControlPointInfo();
				timing.Add(0, new TimingControlPoint { BeatLength = 500 });
				equal(250, ShredGridTicks.Next(timing, 4, 125));
				equal(250, ShredGridTicks.Next(timing, 4, 200));
				timing.Add(220, new TimingControlPoint { BeatLength = 400 });
				equal(220, ShredGridTicks.Next(timing, 4, 200));
				equal(320, ShredGridTicks.Next(timing, 4, 220));
			}),
			("Editor zoom is independent from saved gameplay scroll speed", () =>
			{
				int speed = ShredScrollSpeed.Speed;
				double original = ShredEditorOperations.VisibleTime(1);
				try
				{
					ShredScrollSpeed.Adjust(1);
					equal(original, ShredEditorOperations.VisibleTime(1));
					equal(original / 2, ShredEditorOperations.VisibleTime(2));
					equal(original, ShredEditorOperations.VisibleTime(double.NaN));
				}
				finally { ShredScrollSpeed.Adjust(speed - ShredScrollSpeed.Speed); }
			}),
			("Sustain tail is silent while head and short-note samples remain", () =>
			{
				var note = new ShredHitObject { Fret = 1, Duration = 500, Samples = new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) } };
				using var drawable = new DrawableShredHitObject(note);
				check(!drawable.GetSamples().Any());
				var head = new ShredHoldHead { Parent = note, Samples = note.Samples };
				using var headDrawable = new DrawableShredHoldHead(head);
				equal(1, headDrawable.GetSamples().Count());
				note.Duration = 0;
				equal(1, drawable.GetSamples().Count());
			})
		};
		foreach (var item in cases) { item.run(); Console.WriteLine("PASS " + item.name); }
	}
}
