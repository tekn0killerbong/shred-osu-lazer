using osu.Game.Rulesets.Scoring;

internal static partial class GameplayChecks
{
    public static void RunRevision52()
    {
        int cases = 0;
        foreach (int row in new[] { 0, 1 })
        foreach (int anchors in new[] { 1, 2, 3 })
        foreach (int moving in Enumerable.Range(1, 4 - anchors))
        foreach (bool reverse in new[] { false, true })
        foreach (double offset in new[] { -75d, 0, 75d })
        foreach (bool missBetween in new[] { false, true })
        {
            using var game = new TestSession();
            int key = 100 + row * 9;
            var first = Enumerable.Range(1, anchors).Select(f => game.Add(500, row, f)).ToArray();
            var pressed = Enumerable.Range(1, moving).Select(f => game.Add(1000, row, f)).ToArray();
            var missed = missBetween ? game.Add(1250, row, 1) : null;
            var returned = Enumerable.Range(1, 3).Select(f => game.Add(1750, row, f)).ToArray();
            game.Seek(400);
            // Descending presses make unused higher-numbered frets older than
            // each required prefix, so fret priority does not hide pick limits.
            for (int f = 3; f >= 1; f--) game.Press(key + f - 1);
            game.Seek(500);
            for (int p = 0; p < anchors; p++) game.Press(key + 5 + p);
            if (first.Any(n => !n.HeadHit)) throw new Exception("Anchor chord setup failed");
            game.Seek(1000);
            var picks = Enumerable.Range(anchors, moving).ToArray();
            foreach (int p in picks) game.Press(key + 5 + p);
            if (pressed.Any(n => !n.HeadHit)) throw new Exception("Moving chord setup failed");
            game.Seek(1750 + offset);
            missed?.CheckExpiry();
            foreach (int p in reverse ? picks.Reverse() : picks)
            {
                game.Release(key + 5 + p);
                if (returned.Any(n => n.HeadHit)) throw new Exception("Return bypassed the existing pick buffer");
            }
            game.FlushPicks();
            int hits = returned.Count(n => n.HeadHit);
            if (hits != anchors)
                throw new Exception($"Return capacity must follow held picks: row={row}, anchors={anchors}, released={moving}, hits={hits}, offset={offset}, miss={missBetween}");
            if (missBetween && missed.Result.Type != HitResult.Miss) throw new Exception("Miss fixture did not expire");
            cases++;
        }
        Console.WriteLine($"PASS v52 {cases} return-capacity cases: held picks only, both rows/orders/timing offsets, including after misses");

        foreach (int row in new[] { 0, 1 })
        {
            int key = 100 + row * 9;
            // The user's actual adjacent chord + single-note example. With
            // all three frets held, picks must not bypass the active fret.
            using (var game = new TestSession())
            {
                var first = game.Add(500, row, 3);
                var chord = new[] { game.Add(1000, row, 1), game.Add(1000, row, 2) };
                var single = game.Add(1500, row, 3);
                game.Seek(400);
                for (int f = 1; f <= 3; f++) game.Press(key + f - 1);
                game.Seek(500); game.Press(key + 5);
                game.Seek(1000); game.Press(key + 6); game.Press(key + 7);
                if (!first.HeadHit || chord.Any(n => n.HeadHit)) throw new Exception("Newer outer fret must block the adjacent chord");
                game.Seek(1500); game.Release(key + 6); game.Release(key + 7); game.FlushPicks();
                if (!single.HeadHit || chord.Any(n => n.Result.Type != HitResult.Miss)) throw new Exception("Return should play the priority single after the rejected chord");
            }
            using (var game = new TestSession())
            {
                var first = new[] { game.Add(500, row, 1), game.Add(500, row, 2) };
                var single = game.Add(1000, row, 3);
                var returned = new[] { game.Add(1500, row, 1), game.Add(1500, row, 2) };
                game.Seek(400);
                foreach (int f in new[] { 3, 1, 2 }) game.Press(key + f - 1);
                game.Seek(500); game.Press(key + 5); game.Press(key + 6);
                game.Seek(1000); game.Press(key + 7);
                if (first.Any(n => !n.HeadHit) || single.HeadHit) throw new Exception("Older single must remain blocked by the held priority chord");
                game.Seek(1500); game.Release(key + 7); game.FlushPicks();
                if (returned.Any(n => !n.HeadHit) || single.Result.Type != HitResult.Miss) throw new Exception("Return on two held picks must restore the two-note chord after miss");
            }
            // Existing v40 interior-fret exception legitimately permits this
            // arrangement; do not remove it while correcting the misunderstanding.
            using (var game = new TestSession())
            {
                var first = game.Add(500, row, 2);
                var chord = new[] { game.Add(1000, row, 1), game.Add(1000, row, 3) };
                var single = game.Add(1500, row, 2);
                game.Seek(400);
                foreach (int f in new[] { 1, 3, 2 }) game.Press(key + f - 1);
                game.Seek(500); game.Press(key + 5);
                game.Seek(1000); game.Press(key + 6); game.Press(key + 7);
                game.Seek(1500); game.Release(key + 6); game.Release(key + 7); game.FlushPicks();
                if (!first.HeadHit || chord.Any(n => !n.HeadHit) || !single.HeadHit) throw new Exception("Existing interior-fret exception must remain usable");
            }
        }
        Console.WriteLine("PASS v52 six chord/single alternations: fret priority preserved in both directions and interior exception retained, both rows");
    }
}
