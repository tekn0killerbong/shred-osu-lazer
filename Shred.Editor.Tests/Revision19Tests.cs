using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Mods;
using osu.Game.Rulesets.Shred.Scoring;
using osu.Game.Rulesets.Shred.UI;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

internal static partial class Program
{
    private static void RunRevision19Tests()
    {
        var cases = new (string name, Action run)[]
        {
            ("Early targeting adds 150 ms before Great while late targeting stops at Great", () =>
            {
                foreach (double od in new[] { 0d, 2.5, 5, 7.5, 10 })
                {
                    var windows = new ShredHitWindows(); windows.SetDifficulty(od);
                    double great = windows.WindowFor(HitResult.Great), miss = windows.WindowFor(HitResult.Miss);
                    equal(150, miss - great);
                    foreach (int sign in new[] { -1, 1 })
                    {
                        check(windows.ResultFor(sign * great) == HitResult.Great);
                        check(osu.Game.Rulesets.Shred.Objects.Drawables.ShredInputJudgement.IsWrongPickInWindow(windows, sign * great));
                        check(osu.Game.Rulesets.Shred.Objects.Drawables.ShredInputJudgement.IsWrongPickInWindow(windows, sign * (great + 0.01)) == (sign < 0));
                        check(osu.Game.Rulesets.Shred.Objects.Drawables.ShredInputJudgement.IsWrongPickInWindow(windows, sign * miss) == (sign < 0));
                        check(!osu.Game.Rulesets.Shred.Objects.Drawables.ShredInputJudgement.IsWrongPickInWindow(windows, sign * (miss + 0.01)));
                    }
                    check(windows.ResultForEarlyRelease(-350) == HitResult.Great);
                    check(windows.ResultForEarlyRelease(-350.01) == HitResult.Miss);
                }
            }),
            ("Early outer-window picks miss only one targeted chord member", () =>
            {
                foreach (double offset in new[] { -250d, -100.01 })
                foreach (int row in new[] { 0, 1 })
                {
                    using var game = new TestSession();
                    var first = game.Add(1000, row, 4); var last = game.Add(1000, row, 2);
                    game.Seek(1000 + offset);
                    game.Press(103 + row * 9); game.Press(101 + row * 9); game.Press(105 + row * 9);
                    check(first.HeadJudged && first.Result.Type == HitResult.Miss && !last.HeadJudged);
                    equal(offset, ((ShredJudgementResult)first.Result).PickOffset.Value);
                    game.Press(106 + row * 9);
                    check(last.Result.Type == HitResult.Miss);
                }
            }),
            ("Fret preparation in the Miss margin stays harmless and Great boundaries hit", () =>
            {
                foreach (double offset in new[] { -100d, 100 })
                {
                    using var game = new TestSession();
                    var note = game.Add(1000, 0, 2);
                    game.Seek(750); game.Press(101); check(!note.HeadJudged);
                    game.Seek(1000 + offset); game.Press(105); check(note.HeadHit);
                }
            }),
            ("Picks outside every window preserve the HP chain and leave future notes intact", () =>
            {
                using var game = new TestSession();
                var attack = game.Add(1000, 0, 1);
                var hp = game.Add(2000, 0, 3, ShredPracticeArticulation.HammerPull);
                game.Seek(1000); game.Press(100); game.Press(105); game.Release(105);
                check(attack.HeadHit);
                game.Seek(1749.99); game.Press(106); game.Release(106); check(!hp.HeadJudged);
                game.Seek(2000); game.Press(102); check(hp.HeadHit);
            }),
            ("An accurately armed pick keeps its timestamp when frets resolve by late Great", () =>
            {
                using var game = new TestSession(); var note = game.Add(1000, 0, 2);
                game.Seek(1000); game.Press(105); check(!note.HeadJudged);
                game.Seek(1100); game.Press(101); check(note.HeadHit);
                equal(0, ((ShredJudgementResult)note.Result).PickOffset.Value);
            }),
            ("An early missed LN never becomes a held sustain and an unpaid LN expires after Great", () =>
            {
                using var game = new TestSession();
                var early = game.Add(1000, 0, 1, ShredPracticeArticulation.Pick, 2000);
                var unpaid = game.Add(1000, 1, 1, ShredPracticeArticulation.Pick, 2000);
                game.Seek(800); game.Press(100); game.Press(105);
                check(early.HeadJudged && !early.HeadHit && !early.IsHolding);
                game.Seek(1100); unpaid.CheckExpiry(); check(!unpaid.HeadJudged);
                game.Seek(1100.01); unpaid.CheckExpiry(); check(unpaid.HeadJudged && !unpaid.HeadHit);
                game.Seek(3000); early.CheckExpiry(); check(early.Result.Type == HitResult.Miss);
            }),
            ("Regular miss shakes one receptor while OPEN miss shakes the entire row", () =>
            {
                foreach (int fret in new[] { 0, 3 })
                {
                    using var game = new TestSession(); var note = game.Add(1000, 1, fret);
                    game.Seek(800); if (fret != 0) game.Press(111); game.Press(114);
                    check(note.Result.Type == HitResult.Miss);
                    var shakes = (bool[,])typeof(ShredPlayfield).GetField("shaking", hidden).GetValue(game.Field);
                    for (int i = 0; i < 5; i++)
                    {
                        check(!shakes[0, i]);
                        check(shakes[1, i] == (fret == 0 || i == fret - 1));
                    }
                }
            }),
            ("Wham press animates an empty playfield and release restores its state", () =>
            {
                using var game = new TestSession();
                game.Press((int)ShredAction.Wham);
                typeof(ShredPlayfield).GetMethod("updateWhamDepression", hidden).Invoke(game.Field, null);
                check((bool)typeof(ShredPlayfield).GetField("whamDepressed", hidden).GetValue(game.Field));
                game.Release((int)ShredAction.Wham);
                check(!(bool)typeof(ShredPlayfield).GetField("whamDepressed", hidden).GetValue(game.Field));
            }),
            ("Early wham strikes miss and late wham heads have already expired", () =>
            {
                foreach (double offset in new[] { -200d, 200 })
                {
                    using var game = new TestSession();
                    var note = game.Add(1000, 0, 0, ShredPracticeArticulation.Wham, 1000);
                    game.Seek(1000 + offset); game.Press((int)ShredAction.Wham);
                    check(note.HeadJudged && !note.HeadHit && !note.IsHolding);
                }
            }),
            ("Lower fret top-left corners align below upper centres and hand sections mirror", () =>
            {
                using var field = new ShredPlayfield { RelativeSizeAxes = Axes.None, Size = new Vector2(1000, 720) };
                for (int fret = 1; fret <= 5; fret++)
                {
                    using var lower = new ShredGlyph(Color4.White, shear: field.Skin.Shear(1))
                    { Position = field.Receiver(1, fret), Origin = Anchor.Centre, Size = field.NoteSize(fret) };
                    check(Math.Abs(lower.ScreenSpaceDrawQuad.TopLeft.X - field.Receiver(0, fret).X) < 0.002);
                }
                for (int row = 0; row < 2; row++)
                {
                    float fretCentre = (field.Receiver(row, 1).X + field.Receiver(row, 5).X) / 2;
                    float pickCentre = (field.Receiver(row, 7).X + field.Receiver(row, 10).X) / 2;
                    check(Math.Abs(fretCentre + pickCentre - field.DrawWidth) < 0.002);
                }
            }),
            ("Recorder forwards all three mouse-tool keys while enabled", () =>
            {
                using var recorder = new ToolKeyRecorder(); recorder.Enabled.Value = true;
                foreach (Key key in new[] { Key.Number1, Key.Number2, Key.Number3 })
                    check(!recorder.PressTool(key));
                check(recorder.Enabled.Value);
            }),
            ("The Original Gamer uses OG and Accuracy explains the new Miss margin", () =>
            {
                check(new ShredModGuitarHero().Acronym == "OG");
                var info = new BeatmapInfo(); info.Difficulty.OverallDifficulty = 5;
                var accuracy = new ShredRuleset().GetBeatmapAttributesForDisplay(info, Array.Empty<Mod>()).First();
                check(accuracy.Description.ToString().Contains("150 ms"));
                check(accuracy.AdditionalMetrics[1].Value.ToString() == "−250 to −100 ms");
                check(accuracy.AdditionalMetrics[2].Value.ToString() == "> +100 ms");
            })
        };
        foreach (var item in cases) { item.run(); Console.WriteLine("PASS " + item.name); }
    }

    private partial class ToolKeyRecorder : ShredStepRecorder
    {
        public ToolKeyRecorder() : base(new ShredRuleset().RulesetInfo) { }
        public bool PressTool(Key key)
        {
            var input = new InputState(); input.Keyboard.Keys.SetPressed(key, true);
            return OnKeyDown(new KeyDownEvent(input, key, false));
        }
    }
}
