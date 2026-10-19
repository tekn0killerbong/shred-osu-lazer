using System;
using System.Linq;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Scoring;

internal static partial class GameplayChecks
{
    public static void RunLegacyPickAudit()
    {
        void check(bool value) { if (!value) throw new Exception("Legacy pick assertion"); }
        void equal(double expected, double actual) { if (Math.Abs(expected-actual)>0.000001) throw new Exception($"Legacy pick timing: {expected} != {actual}"); }
        var cases = new (string name, Action run)[]
        {
            ("Combined held-fret and held-pick trills work in both orders directions rows and offsets", () =>
            {
                foreach (int row in new[] { 0, 1 })
                foreach (bool reversed in new[] { false, true })
                foreach (bool fretFirst in new[] { false, true })
                foreach (double gap in new[] { 0d, 10, 30, 90 })
                foreach (var type in new[] { ShredPracticeArticulation.Pick, ShredPracticeArticulation.HammerPull })
                {
                    using var game = new TestSession();
                    int anchor = 1, foreground = 3;
                    if (reversed) { anchor = 3; foreground = 1; }
                    var notes = Enumerable.Range(0, 7).Select(i => game.Add(1000 + i * 500, row,
                        i % 2 == 0 ? anchor : foreground, type)).ToArray();
                    int fret = 100 + row * 9;
                    int pick = 105 + row * 9;
                    game.Seek(1000); game.Press(fret + anchor - 1); game.Press(pick);
                    for (int i = 1; i < notes.Length; i++)
                    {
                        void Fret() { if (i % 2 == 1) game.Press(fret + foreground - 1); else game.Release(fret + foreground - 1); }
                        void Pick() { if (i % 2 == 1) game.Press(pick + 1); else game.Release(pick + 1); }
                        game.Seek(1000 + i * 500);
                        if (fretFirst) Fret(); else Pick();
                        game.Seek(1000 + i * 500 + gap);
                        if (fretFirst) Pick(); else Fret();
                        game.FlushPicks();
                        if (!notes[i].HeadHit) throw new Exception($"Combined trill row={row}, reversed={reversed}, fretFirst={fretFirst}, gap={gap}, type={type}, step={i}");
                        check(notes.Skip(i + 1).All(n => !n.HeadJudged));
                    }
                }
            }),
            ("Physical chord picks validate each arriving fret separately", () =>
            {
                using var game = new TestSession();
                var notes = new[] { game.Add(1000, 0, 1), game.Add(1000, 0, 3), game.Add(1000, 0, 5) };
                game.Seek(1000); game.Press(105); game.Press(106); game.Press(107);
                for (int i = 0; i < 3; i++)
                {
                    game.Seek(1010 + 20 * i); game.Press(100 + i * 2);
                    check(notes.Take(i + 1).All(n => n.HeadHit));
                    check(notes.Skip(i + 1).All(n => !n.HeadJudged));
                }
            }),
            ("Buffered chord return waits for separate frets before and after buffer commitment", () =>
            {
                foreach (double firstDelay in new[] { 10d, 30 })
                {
                    using var game = new TestSession();
                    game.Add(1000, 0, 1); game.Add(1000, 0, 3); game.Add(1500, 0, 5);
                    var left = game.Add(2000, 0, 1); var right = game.Add(2000, 0, 3);
                    var later = game.Add(2300, 0, 3);
                    game.Seek(1000); game.Press(100); game.Press(102); game.Press(105); game.Press(106);
                    game.Seek(1500); game.Press(104); game.Press(107);
                    game.Release(100); game.Release(102);
                    game.Seek(2000); game.Release(107);
                    game.Seek(2005); game.Release(104);
                    game.Seek(2000 + firstDelay); game.Press(100);
                    game.FlushPicks(); check(left.HeadHit && !right.HeadJudged);
                    game.Seek(2080); game.Press(102); check(right.HeadHit && !later.HeadJudged);
                    equal(0, ((ShredJudgementResult)right.Result).PickOffset.Value);
                }
            }),
            ("A partial chord still rejects newer outer priority frets (interior exemption since v40)", () =>
            {
                // Interior held frets were intentionally allowed in v40.
                foreach (int wrong in new[] { 5 })
                {
                    using var game = new TestSession();
                    var left = game.Add(1000, 0, 1); var right = game.Add(1000, 0, 3);
                    game.Seek(990); game.Press(100); game.Press(100 + wrong - 1);
                    game.Seek(1000); game.Press(105); game.Press(106);
                    check(!left.HeadJudged && !right.HeadJudged);
                    game.Seek(1010); game.Release(100 + wrong - 1);
                    check(left.HeadHit && !right.HeadJudged);
                    game.Seek(1020); game.Press(102); check(right.HeadHit);
                }
            }),
            ("Missing frets cannot extend a hammer-pick past late Great", () =>
            {
                using var game = new TestSession();
                game.Add(1000, 0, 1); game.Add(1500, 0, 3);
                var target = game.Add(2000, 0, 1); var later = game.Add(2200, 0, 1);
                game.Seek(1000); game.Press(100); game.Press(105);
                game.Seek(1500); game.Press(102); game.Press(106);
                game.Seek(2000); game.Release(106); game.FlushPicks();
                game.Seek(2100.01); game.Release(102);
                check(target.HeadJudged && !target.HeadHit && !later.HeadJudged);
            }),
            ("Physical replacement of a waiting hammer-pick preserves timing and consumes only its own capacity", () =>
            {
                using var game = new TestSession();
                game.Add(1000, 0, 1); game.Add(1500, 0, 3);
                var target = game.Add(2000, 0, 1); var later = game.Add(2250, 0, 1);
                game.Seek(1000); game.Press(100); game.Press(105);
                game.Seek(1500); game.Press(102); game.Press(106);
                game.Seek(2000); game.Release(106);
                game.Seek(2010); game.Press(107);
                game.Seek(2030); game.Release(102); game.FlushPicks();
                check(target.HeadHit && !later.HeadJudged);
                equal(10, ((ShredJudgementResult)target.Result).PickOffset.Value);
            }),
            ("Combined fret and pick trills support The Original Gamer with physical picks from both rows", () =>
            {
                using var game = new TestSession(hero: true);
                game.Add(1000, 1, 1); game.Add(1500, 1, 3); var target = game.Add(2000, 1, 1);
                game.Seek(1000); game.Press(109); game.Press(105);
                game.Seek(1500); game.Press(111); game.Press(114);
                game.Seek(2000); game.Release(114);
                game.Seek(2010); game.Release(111); game.FlushPicks();
                check(target.HeadHit);
            })
        };
        foreach (var item in cases) { item.run(); Console.WriteLine("PASS " + item.name); }
    }
}
