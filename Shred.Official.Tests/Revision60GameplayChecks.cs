using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Input;
using osu.Game.Rulesets.Shred.Scoring;

internal static partial class GameplayChecks
{
    public static void RunRevision60()
    {
        int checks = 0;
        void check(bool value, string message) { if (!value) throw new Exception("v60: " + message); checks++; }
        foreach (float od in new[] { 0f, 5f, 10f })
        {
            var windows = new ShredHitWindows(); windows.SetDifficulty(od);
            double great = 120 - 9 * od, miss = 180 - 3 * od;
            check(windows.WindowFor(HitResult.Great) == great && windows.WindowFor(HitResult.Miss) == miss, "linear windows");
            foreach (double offset in new[] { -miss - .1, -miss, -great - .1, -great, 0, great, great + .1, miss, miss + .1 })
            {
                using var game = new TestSession();
                var chord = new[] { game.Add(1000, 0, 1, od: od), game.Add(1000, 0, 2, od: od) };
                game.Seek(500); game.Press(100); game.Press(101);
                game.Seek(1000 + offset); game.Press(105); game.Press(106);
                if (Math.Abs(offset) <= great) check(chord.All(n => n.HeadHit), $"great boundary {od}/{offset}");
                else if (offset >= -miss) check(chord.All(n => n.HeadJudged && !n.HeadHit), $"miss band or expiry {od}/{offset}");
                else check(chord.All(n => !n.HeadJudged), "outside early miss leaves chord untouched");
            }
            using (var game = new TestSession())
            {
                var chord = new[] { game.Add(1000, 0, 1, od: od), game.Add(1000, 1, 1, od: od) };
                game.Seek(1000 + great + .1); foreach (var n in chord) n.CheckExpiry();
                check(chord.All(n => !n.HeadJudged), "no automatic miss at Great");
                game.Seek(1000 + miss); chord[0].CheckExpiry();
                check(chord.All(n => n.HeadJudged && !n.HeadHit), "automatic chord miss at outer limit");
            }
        }
        foreach (int count in new[] { 2, 3, 4 })
        foreach (int row in new[] { 0, 1 })
        foreach (bool fretFirst in new[] { true, false })
        foreach (double duration in new[] { 0d, 300d })
        {
            using var game = new TestSession(); int key = 100 + row * 9;
            var chord = Enumerable.Range(1, count).Select(f => game.Add(1000, row, f, duration: duration)).ToArray();
            game.Seek(950);
            for (int i = 0; i < count; i++) game.Press(key + (fretFirst ? i : 5 + i));
            game.Seek(1020);
            for (int i = 0; i < count; i++)
            {
                game.Press(key + (fretFirst ? 5 + i : i));
                if (i < count - 1) check(chord.All(n => !n.HeadJudged), "no partial chord before final criterion");
            }
            check(chord.All(n => n.HeadHit), "complete chord in either fret/pick order, including LN heads");
        }
        foreach (int count in new[] { 2, 3, 4 })
        foreach (int row in new[] { 0, 1 })
        foreach (int anchorIndex in Enumerable.Range(0, count))
        foreach (bool reverse in new[] { false, true })
        foreach (double rate in new[] { .5, 1, 2 })
        {
            using var game = new TestSession(rate: rate); int key = 100 + row * 9;
            var groups = Enumerable.Range(0, 5).Select(i => Enumerable.Range(1, count).Select(f => game.Add(1000 + i * 400, row, f)).ToArray()).ToArray();
            game.Seek(900); for (int f = 0; f < count; f++) game.Press(key + f);
            game.Seek(1000); for (int p = 0; p < count; p++) game.Press(key + 5 + p);
            check(groups[0].All(n => n.HeadHit), "full strum physical start");
            int[] mobile = Enumerable.Range(0, count).Where(p => p != anchorIndex).ToArray();
            if (reverse) mobile = mobile.Reverse().ToArray();
            for (int step = 1; step < groups.Length; step++)
            {
                game.Seek(1000 + step * 400);
                for (int i = 0; i < mobile.Length; i++)
                {
                    if (step % 2 == 1) game.Release(key + 5 + mobile[i]);
                    else game.Press(key + 5 + mobile[i]);
                    if (i < mobile.Length - 1) check(groups[step].All(n => !n.HeadJudged), "wait for whole mobile group");
                }
                if (step % 2 == 1)
                {
                    check(groups[step].All(n => !n.HeadJudged), "release remains buffered");
                    game.FlushPicks();
                }
                check(groups[step].All(n => n.HeadHit), $"repeated full chord {count}/{row}/{anchorIndex}/{reverse}/{rate}/{step}");
            }
        }
        // The return may play any size. Subsequent physical attacks still need
        // the corresponding mobile picks plus the single held anchor.
        foreach (int upper in Enumerable.Range(1, 4))
        foreach (int lower in Enumerable.Range(1, 4))
        foreach (int anchor in Enumerable.Range(0, upper + lower))
        foreach (bool reverse in new[] { false, true })
        {
            using var game = new TestSession();
            var groups = Enumerable.Range(0, 3).Select(i => Enumerable.Range(1, upper).Select(f => game.Add(1000 + 400 * i, 0, f))
                .Concat(Enumerable.Range(1, lower).Select(f => game.Add(1000 + 400 * i, 1, f))).ToArray()).ToArray();
            int[] picks = Enumerable.Range(105, upper).Concat(Enumerable.Range(114, lower)).ToArray();
            game.Seek(900);
            foreach (int f in Enumerable.Range(100, upper).Concat(Enumerable.Range(109, lower))) game.Press(f);
            game.Seek(1000); foreach (int pick in picks) game.Press(pick);
            check(groups[0].All(n => n.HeadHit), "two-row initial chord " + upper + "+" + lower);
            int[] mobile = picks.Where((_, i) => i != anchor).ToArray();
            if (reverse) mobile = mobile.Reverse().ToArray();
            game.Seek(1400); foreach (int pick in mobile) game.Release(pick); game.FlushPicks();
            check(groups[1].All(n => n.HeadHit), "any initial pick can anchor cross-row return: " + picks[anchor]);
            game.Seek(1800); foreach (int pick in mobile) game.Press(pick);
            check(groups[2].All(n => n.HeadHit), "any initial pick can anchor cross-row attack: " + picks[anchor]);
        }
        foreach (int targetSize in new[] { 1, 2, 3, 4, 5 })
        {
            using var game = new TestSession();
            var start = new[] { game.Add(1000, 0, 1), game.Add(1000, 1, 1), game.Add(1000, 1, 2) };
            var target = Enumerable.Range(1, targetSize).Select(f => game.Add(1500, 1, f)).ToArray();
            game.Seek(900); game.Press(100); game.Press(109); game.Press(110);
            game.Seek(1000); game.Press(105); game.Press(114);
            check(start.All(n => !n.HeadJudged), "cross-row chord remains atomic");
            game.Press(115); check(start.All(n => n.HeadHit), "cross-row full chord");
            game.Seek(1400); game.Release(100);
            for (int f = 2; f < targetSize; f++) game.Press(109 + f);
            for (int f = targetSize; f < 2; f++) game.Release(109 + f);
            game.Seek(1500); game.Release(114); game.Release(115); game.FlushPicks();
            check(target.All(n => n.HeadHit), "cross-row return to changed size " + targetSize);
        }
        foreach (bool anchorFirst in new[] { true, false })
        foreach (double releaseGap in new[] { 0d, 10d, 30d })
        {
            using var game = new TestSession();
            var first = new[] { game.Add(1000, 0, 1), game.Add(1000, 0, 2), game.Add(1000, 0, 3) };
            var next = new[] { game.Add(1500, 0, 1), game.Add(1500, 0, 2), game.Add(1500, 0, 3) };
            game.Seek(900); game.Press(100); game.Press(101); game.Press(102);
            game.Seek(1000); game.Press(105); game.Press(106); game.Press(107);
            game.Seek(1500);
            if (anchorFirst) game.Release(105);
            game.Release(106); game.Release(107);
            if (!anchorFirst) { game.Seek(1500 + releaseGap); game.Release(105); }
            game.FlushPicks(); check(next.All(n => !n.HeadJudged), "release all cancels pending special return");
            game.Press(105); game.Press(106); game.Press(107);
            check(next.All(n => n.HeadHit), "ordinary attack after ending shred");
        }
        using (var game = new TestSession())
        {
            var overdue = new[] { game.Add(1000, 0, 1), game.Add(1000, 0, 2) };
            var next = new[] { game.Add(1100, 0, 1), game.Add(1100, 0, 2) };
            game.Seek(900); game.Press(100); game.Press(101);
            game.Seek(1100); game.Press(105); game.Press(106);
            check(next.All(n => n.HeadHit) && overdue.All(n => !n.HeadJudged), "waiting miss does not lock next chord");
            game.Seek(1165); overdue[0].CheckExpiry();
            check(overdue.All(n => n.HeadJudged && !n.HeadHit), "overdue chord eventually misses as a whole");
        }
        foreach (int nextSize in new[] { 1, 2, 3, 4 })
        {
            using var game = new TestSession();
            var first = Enumerable.Range(1, 4).Select(f => game.Add(1000, 0, f)).ToArray();
            var returned = Enumerable.Range(1, nextSize).Select(f => game.Add(1500, 0, f)).ToArray();
            var attacked = Enumerable.Range(1, nextSize).Select(f => game.Add(2000, 0, f)).ToArray();
            var following = Enumerable.Range(1, nextSize).Select(f => game.Add(2040, 0, f)).ToArray();
            game.Seek(900); for (int i = 0; i < 4; i++) game.Press(100 + i);
            game.Seek(1000); for (int i = 0; i < 4; i++) game.Press(105 + i);
            game.Seek(1400); for (int i = nextSize; i < 4; i++) game.Release(100 + i);
            game.Seek(1500); game.Release(106); game.Release(107); game.Release(108); game.FlushPicks();
            check(returned.All(n => n.HeadHit), "return changes cardinality to " + nextSize);
            game.Seek(2000);
            for (int i = 0; i < Math.Max(1, nextSize - 1); i++)
            {
                game.Press(106 + i);
                if (i < Math.Max(1, nextSize - 1) - 1) check(attacked.All(n => !n.HeadJudged), "changed-size attack waits for final pick");
            }
            check(attacked.All(n => n.HeadHit), "attack adapts to new cardinality " + nextSize);
            for (int i = Math.Max(1, nextSize - 1); i < 3; i++) game.Press(106 + i);
            check(following.All(n => !n.HeadJudged), "excess mobile picks cannot spill to following chord");
        }
        foreach (double late in new[] { -100d, 100d })
        {
            using var game = new TestSession();
            var hp = new[] { game.Add(1000, 0, 1, ShredPracticeArticulation.HammerPull), game.Add(1000, 0, 2, ShredPracticeArticulation.HammerPull) };
            game.Seek(1000 + late); game.Press(100); game.Press(101);
            check(hp.All(n => n.HeadJudged && !n.HeadHit), "fret HP input in miss band immediately misses whole chord");
        }
        using (var game = new TestSession())
        {
            var chord = new[] { game.Add(1000, 0, 1), game.Add(1000, 0, 2) };
            game.Seek(1000); game.Press(100); game.Press(105); game.Press(106);
            check(chord.All(n => !n.HeadJudged), "missing fret leaves entire chord pending");
            game.Seek(1080); game.Press(101);
            check(chord.All(n => !n.HeadHit), "late fret does not extend Great despite an earlier valid pick");
            game.Seek(1165); chord[0].CheckExpiry();
            check(chord.All(n => n.HeadJudged && !n.HeadHit), "incomplete chord eventually misses together");
        }
        using (var game = new TestSession())
        {
            var groups = new[] { 1000, 1400, 1800, 2200 }.Select(t => Enumerable.Range(1, 3).Select(f => game.Add(t, 0, f)).ToArray()).ToArray();
            game.Seek(900); game.Press(100); game.Press(101); game.Press(102);
            game.Seek(1000); game.Press(105); game.Press(106); game.Press(107);
            game.Seek(1400); game.Release(106); game.Release(107); game.FlushPicks();
            game.Seek(1800); game.Press(106);
            check(groups[2].All(n => !n.HeadJudged), "incomplete full reattack is atomic");
            game.Seek(1965); groups[2][0].CheckExpiry();
            game.Seek(2200); game.Press(107);
            check(groups[3].All(n => !n.HeadJudged), "missed chord's old pick cannot fund next reattack");
            game.Release(106); game.Press(106);
            check(groups[3].All(n => n.HeadHit), "fresh matching mobile group can recover");
        }
        foreach (bool pause in new[] { true, false })
        {
            using var game = new TestSession();
            foreach (int t in new[] { 1000, 1400 }) for (int f = 1; f <= 3; f++) game.Add(t, 0, f);
            var next = Enumerable.Range(1, 3).Select(f => game.Add(1800, 0, f)).ToArray();
            game.Seek(900); game.Press(100); game.Press(101); game.Press(102);
            game.Seek(1000); game.Press(105); game.Press(106); game.Press(107);
            game.Seek(1400); game.Release(106); game.Release(107); game.FlushPicks();
            game.Seek(1800); game.Press(106); game.Press(107);
            var pending = Enumerable.Range(1, 3).Select(f => game.Add(2200, 0, f)).ToArray();
            game.Seek(2200); game.Release(106); game.Release(107);
            if (pause) game.Ruleset.IsPaused.Value = true;
            else game.Seek(2100);
            game.Field.ProcessPendingPicks();
            game.Ruleset.IsPaused.Value = false;
            game.Seek(2240); game.Field.ProcessPendingPicks();
            check(pending.All(n => !n.HeadJudged), "pending full return cancelled on " + (pause ? "pause" : "rewind"));
        }
        foreach (double rate in new[] { .5, 1, 2 })
        {
            using var game = new TestSession(rate: rate);
            var groups = new[] { 1000, 1040, 1080 }.Select(t => Enumerable.Range(1, 3).Select(f => game.Add(t, 0, f)).ToArray()).ToArray();
            game.Seek(900); game.Press(100); game.Press(101); game.Press(102);
            game.Seek(1000); game.Press(105); game.Press(106); game.Press(107);
            game.Seek(1040); game.Release(106); game.Release(107);
            game.Seek(1050); game.Field.ProcessPendingPicks();
            check(groups[1].All(n => !n.HeadJudged), "dense release preserves strict buffer boundary");
            game.Seek(1050.01); game.Field.ProcessPendingPicks();
            check(groups[1].All(n => n.HeadHit), "dense release commits at adapted 10 map-ms buffer");
            game.Seek(1080); game.Press(106); game.Press(107);
            check(groups[2].All(n => n.HeadHit), "next rapid physical attack remains playable at rate " + rate);
        }
        check(ShredPickBuffer<int>.WindowFor(40, 1) == 10 && ShredPickBuffer<int>.WindowFor(1000, 2) == 60, "adaptive real-time buffer");
        Console.WriteLine($"PASS v60: {checks} judgement, full-strum, release cancellation and timing checks");
    }
}
