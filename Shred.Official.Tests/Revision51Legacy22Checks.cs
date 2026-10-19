using System;
using System.Linq;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Input;
using osu.Game.Rulesets.Shred.Scoring;

internal static partial class GameplayChecks
{
    public static void RunLegacy22Audit()
    {
        void check(bool value) { if (!value) throw new Exception("Legacy pick assertion"); }
        void equal(double expected, double actual) { if (Math.Abs(expected-actual)>0.000001) throw new Exception($"Legacy pick timing: {expected} != {actual}"); }
        var cases = new (string name, Action run)[]
        {
            ("Judgement colour follows every crossed grid tick and survives skipped frames and seeking", () =>
            {
                var timing = new ControlPointInfo();
                timing.Add(0, new TimingControlPoint { BeatLength = 500 });
                foreach (int divisor in new[] { 1, 2, 4, 8, 3, 6, 5, 7 })
                {
                    var ticks = ShredGridTicks.Enumerate(timing, divisor, 0, 2000).ToArray();
                    foreach (var tick in ticks.Reverse())
                    {
                        equal(tick.Divisor, ShredGridTicks.DivisorAt(timing, divisor, tick.Time));
                        equal(tick.Divisor, ShredGridTicks.DivisorAt(timing, divisor, tick.Time + 500d / divisor * 0.9));
                    }
                }
            }),
            ("Judgement colour resets on an off-grid timing change and before the first beat", () =>
            {
                var timing = new ControlPointInfo();
                timing.Add(100, new TimingControlPoint { BeatLength = 500 });
                timing.Add(320, new TimingControlPoint { BeatLength = 400 });
                equal(1, ShredGridTicks.DivisorAt(timing, 4, 50));
                equal(4, ShredGridTicks.DivisorAt(timing, 4, 319));
                equal(1, ShredGridTicks.DivisorAt(timing, 4, 320));
                equal(4, ShredGridTicks.DivisorAt(timing, 4, 420));
                equal(2, ShredGridTicks.DivisorAt(timing, 4, 520));
                equal(1, ShredGridTicks.DivisorAt(timing, 4, 100));
            }),
            ("Picks reverse priority like frets even while targeting the same unfinished chord", () =>
            {
                foreach (int anchor in Enumerable.Range(0, 8))
                foreach (int right in Enumerable.Range(0, 8).Where(i => i != anchor))
                foreach (int left in Enumerable.Range(0, 8).Where(i => i != anchor && i != right))
                {
                    var picks = new ShredPickSequence();
                    picks.Press(anchor / 4, anchor % 4, 1000, 1000);
                    picks.Press(right / 4, right % 4, 1000, 1050);
                    picks.Press(left / 4, left % 4, 1000, 1100);
                    equal(1 << right, picks.Release(left / 4, left % 4));
                    equal(1 << anchor, picks.Release(right / 4, right % 4));
                    equal(0, picks.Release(anchor / 4, anchor % 4));
                }
            }),
            ("Chord grouping ends after twenty real milliseconds without sliding on each press", () =>
            {
                foreach (double rate in new[] { 0d, 0.5, 1, 1.5, 2 })
                {
                    double effectiveRate = rate > 0 ? rate : 1;
                    var picks = new ShredPickSequence();
                    picks.Press(0, 0, 1000, 1000, rate);
                    picks.Press(0, 1, 1000, 1000 + 20 * effectiveRate, rate);
                    picks.Press(0, 2, 1000, 1000 + 30 * effectiveRate, rate);
                    equal(3, picks.Release(0, 2));
                    equal(0, picks.Release(0, 0));
                    equal(0, picks.Release(0, 1));
                }
            }),
            ("Repressing a released chord member always takes fresh priority", () =>
            {
                var picks = new ShredPickSequence();
                picks.Press(0, 0, 1000, 1000);
                picks.Press(0, 1, 1000, 1001);
                equal(0, picks.Release(0, 1));
                picks.Press(0, 1, 1000, 1002);
                equal(1, picks.Release(0, 1));
                equal(0, picks.Release(0, 0));
            }),
            ("A directional return after a staggered physical chord plays the next note", () =>
            {
                using var game = new TestSession();
                game.Add(1000, 0, 2);
                var chord = new[] { game.Add(1500, 0, 1), game.Add(1500, 0, 2) };
                var returned = game.Add(2000, 0, 2);
                game.Seek(1000); game.Press(100); game.Press(101); game.Press(107);
                game.Seek(1450); game.Press(106);
                game.Seek(1500); game.Press(105); check(chord.All(n => n.HeadHit));
                game.Seek(2000); game.Release(105); game.FlushPicks();
                check(returned.HeadHit);
                equal(0, ((ShredJudgementResult)returned.Result).PickOffset.Value);
            }),
            ("A physical pick overrides a pending return and keeps its own judgement timestamp", () =>
            {
                foreach (bool reverse in new[] { false, true })
                {
                    using var game = new TestSession();
                    game.Add(1000, 0, 1); game.Add(1500, 0, 1);
                    var target = game.Add(2000, 0, 1); var later = game.Add(2250, 0, 1);
                    game.Seek(1000); game.Press(100); game.Press(105);
                    game.Seek(1500); game.Press(107);
                    game.Seek(1995);
                    if (reverse) game.Press(106); else game.Release(107);
                    game.Seek(2005);
                    if (reverse) game.Release(107); else game.Press(106);
                    game.FlushPicks();
                    check(target.HeadHit && !later.HeadJudged);
                    double expected = 5;
                    if (reverse) expected = -5;
                    equal(expected, ((ShredJudgementResult)target.Result).PickOffset.Value);
                }
            }),
            ("A cascade of releases cannot queue ultra-rapid synthetic picks", () =>
            {
                var buffer = new ShredPickBuffer<int>();
                int committed = 0;
                check(buffer.Queue(1000, 20, 1));
                check(!buffer.Queue(1005, 20, 2));
                check(!buffer.Queue(1020, 20, 4));
                buffer.Flush(1021, (value, time) => committed += value);
                equal(1, committed);
                check(buffer.Queue(1021, 20, 8));
                buffer.PhysicalPress(1025, (value, time) => committed += value);
                buffer.Flush(1100, (value, time) => committed += value);
                equal(1, committed);
            })
        };
        foreach (var item in cases) { item.run(); Console.WriteLine("PASS " + item.name); }
    }
}
