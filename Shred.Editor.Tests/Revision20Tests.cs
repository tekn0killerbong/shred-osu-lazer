using System;
using System.Linq;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred.Input;
using osu.Game.Rulesets.Shred.Gameplay;

internal static partial class Program
{
    private static void RunRevision20Tests()
    {
        var cases = new (string name, Action run)[]
        {
            ("Pick groups return to their anchor only after the last release in every order", () =>
            {
                int[][] orders = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
                foreach (var order in orders)
                {
                    var picks = new ShredPickSequence(); picks.Press(0, 0, 1000);
                    for (int i = 0; i < 3; i++) picks.Press(1, i, 1500);
                    equal(0, picks.Release(1, order[0])); equal(0, picks.Release(1, order[1]));
                    equal(1, picks.Release(1, order[2])); equal(0, picks.Release(1, order[2]));
                    equal(0, picks.Release(0, 0));
                }
            }),
            ("Pick priority ignores repeats and releases of background gestures", () =>
            {
                var picks = new ShredPickSequence();
                picks.Press(0, 0, 1000); picks.Press(0, 1, 1500);
                picks.Press(0, 0, 2000); // Already held: no new edge or priority.
                equal(0, picks.Release(0, 0)); equal(0, picks.Release(0, 1));
                picks.Press(1, 0, 2500); picks.Press(0, 0, 3000);
                picks.Reset(); equal(0, picks.Release(0, 0));
            }),
            ("One held top pick trills against three bottom picks repeatedly", () =>
            {
                using var game = new TestSession();
                var top = new[] { game.Add(1000, 0, 3), game.Add(2000, 0, 3), game.Add(3000, 0, 3) };
                var bottom = new[] { 1500d, 2500d }.SelectMany(t => Enumerable.Range(1, 3).Select(f => game.Add(t, 1, f))).ToArray();
                game.Seek(500); foreach (int key in new[] { 109, 110, 111, 102 }) game.Press(key);
                game.Seek(1000); game.Press(105); check(top[0].HeadHit);
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    game.Seek(1500 + 1000 * cycle);
                    game.Press(114); game.Press(115); game.Press(116);
                    check(bottom.Skip(3 * cycle).Take(3).All(n => n.HeadHit));
                    game.Seek(2000 + 1000 * cycle);
                    int[] order = cycle == 0 ? new[] { 114, 115, 116 } : new[] { 116, 115, 114 };
                    game.Release(order[0]); game.Release(order[1]); check(!top[cycle + 1].HeadJudged);
                    game.Release(order[2]); game.FlushPicks(); check(top[cycle + 1].HeadHit);
                }
            }),
            ("Same-row hammer-pick trill plays on press and return to the held pick", () =>
            {
                using var game = new TestSession();
                var notes = Enumerable.Range(0, 5).Select(i => game.Add(1000 + i * 500, 0, 1)).ToArray();
                game.Seek(1000); game.Press(100); game.Press(105);
                game.Seek(1500); game.Press(106);
                game.Seek(2000); game.Release(106);
                game.Seek(2500); game.Press(106);
                game.Seek(3000); game.Release(106); game.FlushPicks();
                check(notes.All(n => n.HeadHit));
            }),
            ("Returning to three held picks pays three notes on their own row", () =>
            {
                using var game = new TestSession();
                var anchor = Enumerable.Range(1, 3).Select(f => game.Add(1000, 1, f)).ToArray();
                var upper = game.Add(1500, 0, 3);
                var returned = Enumerable.Range(1, 3).Select(f => game.Add(2000, 1, f)).ToArray();
                game.Seek(500); foreach (int key in new[] { 109, 110, 111, 102 }) game.Press(key);
                game.Seek(1000); game.Press(114); game.Press(115); game.Press(116); check(anchor.All(n => n.HeadHit));
                game.Seek(1500); game.Press(105); check(upper.HeadHit);
                game.Seek(2000); game.Release(105); game.FlushPicks(); check(returned.All(n => n.HeadHit));
            }),
            ("Two retained picks cannot pay for three simultaneous notes", () =>
            {
                using var game = new TestSession();
                game.Add(1000, 1, 1); game.Add(1000, 1, 2); game.Add(1500, 0, 2);
                var returned = Enumerable.Range(1, 3).Select(f => game.Add(2000, 1, f)).ToArray();
                game.Seek(500); foreach (int key in new[] { 109, 110, 101 }) game.Press(key);
                game.Seek(1000); game.Press(114); game.Press(115);
                game.Seek(1500); game.Press(105);
                game.Seek(2000); game.Press(111); game.Release(105); game.FlushPicks();
                check(returned[0].HeadHit && returned[1].HeadHit && !returned[2].HeadJudged);
                game.Release(105); check(!returned[2].HeadJudged);
                game.Seek(2300); returned[2].CheckExpiry(); check(returned[2].Result.Type == HitResult.Miss);
            }),
            ("A revealed cross-row chord retains each row's separate pick count", () =>
            {
                using var game = new TestSession();
                var firstTop = game.Add(1000, 0, 2);
                game.Add(1000, 1, 1); game.Add(1000, 1, 2); game.Add(1500, 1, 2);
                var top = game.Add(2000, 0, 2);
                var bottom = new[] { game.Add(2000, 1, 1), game.Add(2000, 1, 2) };
                game.Seek(500); foreach (int key in new[] { 109, 110, 101 }) game.Press(key);
                game.Seek(1000); game.Press(105); game.Press(114); game.Press(115); check(firstTop.HeadHit);
                game.Seek(1500); game.Press(116);
                game.Seek(2000); game.Release(116); game.FlushPicks();
                check(top.HeadHit && bottom.All(n => n.HeadHit));
            }),
            ("A top-row anchor cannot strum a bottom-row note on release", () =>
            {
                using var game = new TestSession();
                game.Add(1000, 0, 1); game.Add(1500, 1, 1); var next = game.Add(2000, 1, 1);
                game.Seek(500); game.Press(100); game.Press(109);
                game.Seek(1000); game.Press(105); game.Seek(1500); game.Press(114);
                game.Seek(2000); game.Release(114); check(!next.HeadJudged);
            }),
            ("Reactivated pick capacity cannot spill into the following chord", () =>
            {
                using var game = new TestSession();
                foreach (int f in new[] { 1, 2, 3 }) game.Add(1000, 1, f);
                game.Add(1500, 0, 3);
                var next = game.Add(2000, 1, 3); var later = game.Add(2050, 1, 3);
                game.Seek(500); foreach (int key in new[] { 109, 110, 111, 102 }) game.Press(key);
                game.Seek(1000); game.Press(114); game.Press(115); game.Press(116);
                game.Seek(1500); game.Press(105);
                game.Seek(2000); game.Release(105); game.FlushPicks(); check(next.HeadHit && !later.HeadJudged);
            }),
            ("A miss breaks hammer-picks until another physical strum succeeds", () =>
            {
                using var game = new TestSession();
                game.Add(1000, 0, 1); game.Add(1500, 1, 1); var missed = game.Add(1750, 1, 1);
                var next = game.Add(2250, 0, 1);
                game.Seek(500); game.Press(100); game.Press(109);
                game.Seek(1000); game.Press(105); game.Seek(1500); game.Press(114);
                game.Seek(2250); game.Release(114);
                check(missed.Result.Type == HitResult.Miss && !next.HeadJudged);
                game.Press(106); check(next.HeadHit);
            }),
            ("A pull outside Great or with incorrect frets cannot arm a future hit", () =>
            {
                foreach (bool wrongFret in new[] { false, true })
                {
                    using var game = new TestSession();
                    game.Add(1000, 0, 1); game.Add(1500, 1, 1); var next = game.Add(2000, 0, 1);
                    game.Seek(500); game.Press(100); game.Press(109);
                    game.Seek(1000); game.Press(105); game.Seek(1500); game.Press(114);
                    game.Seek(wrongFret ? 2000 : 1750); if (wrongFret) game.Release(100);
                    game.Release(114); check(!next.HeadJudged);
                    game.Seek(2000); if (wrongFret) game.Press(100);
                    check(!next.HeadJudged);
                }
            }),
            ("Hammer-picks preserve held LN tails while attacking another fret", () =>
            {
                using var game = new TestSession();
                var sustain = game.Add(1000, 0, 1, ShredPracticeArticulation.Pick, 3000);
                var attack = game.Add(1500, 0, 2); var returned = game.Add(2000, 0, 2);
                game.Seek(1000); game.Press(100); game.Press(105); check(sustain.IsHolding);
                game.Seek(1500); game.Press(101); game.Press(106); check(attack.HeadHit);
                game.Seek(2000); game.Release(106); game.FlushPicks(); check(returned.HeadHit && sustain.IsHolding);
            }),
            ("The Original Gamer maps hammer-picks from either physical row to its playable row", () =>
            {
                using var game = new TestSession(hero: true);
                var notes = new[] { game.Add(1000, 1, 1), game.Add(1500, 1, 1), game.Add(2000, 1, 1) };
                game.Seek(1000); game.Press(109); game.Press(105);
                game.Seek(1500); game.Press(114);
                game.Seek(2000); game.Release(114); game.FlushPicks();
                check(notes.All(n => n.HeadHit));
            }),
            ("Paused releases and releases after rewinding never produce phantom picks", () =>
            {
                foreach (bool paused in new[] { false, true })
                {
                    using var game = new TestSession();
                    game.Add(1000, 0, 1); game.Add(1500, 1, 1); var next = game.Add(2000, 0, 1);
                    game.Seek(500); game.Press(100); game.Press(109);
                    game.Seek(1000); game.Press(105); game.Seek(1500); game.Press(114);
                    if (paused) { game.Seek(2000); game.Ruleset.IsPaused.Value = true; }
                    else game.Seek(1400);
                    game.Release(114);
                    game.Ruleset.IsPaused.Value = false; game.Seek(2000); game.Release(114);
                    check(!next.HeadJudged);
                }
            })
        };
        foreach (var item in cases) { item.run(); Console.WriteLine("PASS " + item.name); }
    }
}
