using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Shred.Input;
using osu.Game.Rulesets.Shred.Scoring;

internal static partial class Program
{
    private static void RunRevision21Tests()
    {
        var cases = new (string name, Action run)[]
        {
            ("Every disjoint pair of held pick groups returns the exact anchor mask", () =>
            {
                for (int anchor = 1; anchor < 256; anchor++)
                for (int foreground = 1; foreground < 256; foreground++)
                {
                    if ((anchor & foreground) != 0) continue;
                    var picks = new ShredPickSequence();
                    for (int i = 0; i < 8; i++) if ((anchor & (1 << i)) != 0) picks.Press(i / 4, i % 4, 1000);
                    var raised = Enumerable.Range(0, 8).Where(i => (foreground & (1 << i)) != 0).ToArray();
                    foreach (int i in raised) picks.Press(i / 4, i % 4, 1500);
                    for (int i = 0; i < raised.Length; i++)
                        equal(i == raised.Length - 1 ? anchor : 0, picks.Release(raised[i] / 4, raised[i] % 4));
                }
            }),
            ("Physical and synthetic edges within the buffer merge in both orders including its boundary", () =>
            {
                foreach (double delta in new[] { 0d, 0.1, 1, 5, 19.99, 20 })
                foreach (bool physicalFirst in new[] { false, true })
                {
                    var buffer = new ShredPickBuffer<int>(); int committed = 0;
                    void Commit(int count, double time) { committed += count; }
                    if (physicalFirst)
                    {
                        buffer.PhysicalPress(1000, Commit);
                        check(!buffer.Queue(1000 + delta, 20, 3));
                    }
                    else
                    {
                        check(buffer.Queue(1000, 20, 3));
                        buffer.Flush(1000 + delta, Commit);
                        buffer.PhysicalPress(1000 + delta, Commit);
                    }
                    buffer.Flush(1100, Commit); equal(0, committed);
                }
            }),
            ("Beyond the buffer distinct gestures survive with their original timestamps", () =>
            {
                var buffer = new ShredPickBuffer<int>(); var times = new List<double>();
                void Commit(int value, double time) { times.Add(time); }
                buffer.Queue(1000, 20, 1); buffer.PhysicalPress(1020.01, Commit);
                check(times.SequenceEqual(new[] { 1000d }));
                check(buffer.Queue(1040.02, 20, 2)); buffer.Flush(1100, Commit);
                check(times.SequenceEqual(new[] { 1000d, 1040.02 }));
                equal(20, ShredPickBuffer<int>.WindowFor(500));
                equal(10, ShredPickBuffer<int>.WindowFor(40));
                equal(5, ShredPickBuffer<int>.WindowFor(20));
                equal(30, ShredPickBuffer<int>.WindowFor(500, 1.5));
            }),
            ("Invalid clock rates retain a normal buffer and valid rates scale it", () =>
            {
                foreach (double rate in new[] { 0d, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                {
                    equal(20, ShredPickBuffer<int>.WindowFor(500, rate));
                    equal(10, ShredPickBuffer<int>.WindowFor(40, rate));
                }
                equal(10, ShredPickBuffer<int>.WindowFor(500, 0.5));
                equal(20, ShredPickBuffer<int>.WindowFor(500, 1));
                equal(30, ShredPickBuffer<int>.WindowFor(500, 1.5));
                equal(40, ShredPickBuffer<int>.WindowFor(500, 2));
            }),
            ("Release then physical press cannot accidentally miss the upcoming note", () =>
            {
                foreach (double rate in new[] { 0d, 0.5, 1, 1.5, 2 })
                foreach (double delta in new[] { 0d, 1, 10, 20 })
                foreach (bool reverse in new[] { false, true })
                {
                    using var game = new TestSession(rate: rate);
                    double effectiveRate = rate > 0 ? rate : 1;
                    game.Add(1000, 0, 1); game.Add(1500, 0, 1);
                    var current = game.Add(2000, 0, 1); var upcoming = game.Add(2000 + 150 * effectiveRate, 0, 1);
                    game.Seek(1000); game.Press(100); game.Press(105);
                    game.Seek(1500); game.Press(106);
                    game.Seek(2000);
                    if (reverse) game.Press(107); else game.Release(106);
                    game.Seek(2000 + delta * effectiveRate);
                    if (reverse) game.Release(106); else game.Press(107);
                    game.FlushPicks();
                    if (!current.HeadHit || upcoming.HeadJudged)
                        throw new Exception($"Double-pick guard: configuredRate={rate}, observedRate={game.Field.Clock.Rate}, deltaRealMs={delta}, physicalFirst={reverse}, currentHit={current.HeadHit}, currentResult={current.Result.Type}, nextJudged={upcoming.HeadJudged}, nextResult={upcoming.Result.Type}");
                }
            }),
            ("Distinct regular picks keep simultaneous chords and very rapid notes", () =>
            {
                using var game = new TestSession();
                var first = game.Add(1000, 0, 1); var second = game.Add(1000, 0, 2);
                var fast = game.Add(1005, 0, 2);
                game.Seek(1000); game.Press(100); game.Press(101); game.Press(105); game.Press(106);
                check(first.HeadHit && second.HeadHit);
                game.Seek(1005); game.Press(107); check(fast.HeadHit);
            }),
            ("Dense note spacing shortens the buffer so a genuine following press survives", () =>
            {
                using var game = new TestSession();
                game.Add(1000, 0, 1); game.Add(1500, 0, 1);
                var current = game.Add(2000, 0, 1); var next = game.Add(2060, 0, 1);
                game.Seek(1000); game.Press(100); game.Press(105); game.Seek(1500); game.Press(106);
                game.Seek(2000); game.Release(106); check(!current.HeadJudged);
                game.Seek(2016); game.Press(107);
                check(current.HeadHit && next.HeadHit);
                equal(0, ((ShredJudgementResult)current.Result).PickOffset.Value);
                equal(-44, ((ShredJudgementResult)next.Result).PickOffset.Value);
            }),
            ("Buffered pulls keep release-time accuracy and fret eligibility", () =>
            {
                using var game = new TestSession();
                game.Add(1000, 0, 1); game.Add(1500, 0, 1); var next = game.Add(2000, 0, 1);
                game.Seek(1000); game.Press(100); game.Press(105); game.Seek(1500); game.Press(106);
                game.Seek(2090); game.Release(106); check(!next.HeadJudged);
                game.Seek(2095); game.Release(100);
                game.FlushPicks(); check(next.HeadHit);
                equal(90, ((ShredJudgementResult)next.Result).PickOffset.Value);
            }),
            ("A near-simultaneous physical press on the other row cancels the synthetic gesture", () =>
            {
                using var game = new TestSession();
                game.Add(1000, 0, 1); game.Add(1500, 1, 1);
                var top = game.Add(2000, 0, 1); var bottom = game.Add(2000, 1, 1);
                game.Seek(500); game.Press(100); game.Press(109);
                game.Seek(1000); game.Press(105); game.Seek(1500); game.Press(114);
                game.Seek(2000); game.Release(114); game.Seek(2001); game.Press(115); game.FlushPicks();
                check(!top.HeadJudged && bottom.HeadHit);
            }),
            ("Pending pulls are discarded on pause and rewind", () =>
            {
                foreach (bool pause in new[] { false, true })
                {
                    using var game = new TestSession();
                    game.Add(1000, 0, 1); game.Add(1500, 0, 1); var next = game.Add(2000, 0, 1);
                    game.Seek(1000); game.Press(100); game.Press(105); game.Seek(1500); game.Press(106);
                    game.Seek(2000); game.Release(106); check(!next.HeadJudged);
                    if (pause) game.Ruleset.IsPaused.Value = true; else game.Seek(1900);
                    game.Field.ProcessPendingPicks(); game.Ruleset.IsPaused.Value = false;
                    game.Seek(2100); game.Field.ProcessPendingPicks(); check(!next.HeadJudged);
                }
            })
        };
        foreach (var item in cases) { item.run(); Console.WriteLine("PASS " + item.name); }
    }
}
