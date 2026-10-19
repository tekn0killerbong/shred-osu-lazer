using osu.Game.Rulesets.Shred.Objects;

internal static partial class GameplayChecks
{
    public static void RunRevision61()
    {
        int count = 0;
        void check(bool passed, string label) { if (!passed) throw new Exception(label); count++; }
        foreach (bool failedAttack in new[] { true, false })
        foreach (bool wrongFrets in new[] { true, false })
        foreach (int anchor in new[] { 105, 114, 115 })
        {
            using var game = new TestSession();
            var groups = Enumerable.Range(0, 6).Select(i => new[] { game.Add(1000 + i * 400, 0, 1), game.Add(1000 + i * 400, 1, 1), game.Add(1000 + i * 400, 1, 2) }).ToArray();
            var picks = new[] { 105, 114, 115 }.Where(p => p != anchor).ToArray();
            game.Seek(900); game.Press(100); game.Press(109); game.Press(110);
            game.Seek(1000); game.Press(105); game.Press(114); game.Press(115);
            for (int i = 1; i < groups.Length; i++)
            {
                bool fail = i == (failedAttack ? 2 : 1);
                if (fail && wrongFrets) { game.Seek(1000 + i * 400 - 100); game.Release(110); }
                game.Seek(1000 + i * 400 + (fail && !wrongFrets ? 100 : 0));
                foreach (int p in picks) { if (i % 2 == 1) game.Release(p); else game.Press(p); }
                if (i % 2 == 1) game.FlushPicks();
                if (fail)
                {
                    game.Seek(1000 + i * 400 + 165); groups[i][0].CheckExpiry();
                    check(groups[i].All(n => n.HeadJudged && !n.HeadHit), "intended chord misses completely");
                    if (wrongFrets) game.Press(110);
                }
                else check(groups[i].All(n => n.HeadHit), $"shred recovers on next {(i % 2 == 1 ? "release" : "press")}; failedAttack={failedAttack}, wrongFrets={wrongFrets}, anchor={anchor}, i={i}");
            }
        }
        foreach (double offset in new[] { -.95, .95 })
        {
            var raw = new osu.Game.Beatmaps.Beatmap<ShredHitObject>();
            foreach (var lane in new[] { (row: 0, fret: 1), (row: 1, fret: 1), (row: 1, fret: 2) })
            {
                raw.HitObjects.Add(new ShredHitObject { StartTime = 1000, Duration = 400, Row = lane.row, Fret = lane.fret });
                raw.HitObjects.Add(new ShredHitObject { StartTime = 1000 + offset, Duration = Math.Abs(offset), Row = lane.row, Fret = lane.fret });
            }
            var playable = new osu.Game.Beatmaps.FlatWorkingBeatmap(raw).GetPlayableBeatmap(new osu.Game.Rulesets.Shred.ShredRuleset().RulesetInfo);
            var repaired = playable.HitObjects.Cast<ShredHitObject>().ToArray();
            check(raw.HitObjects.Count == 6 && repaired.Length == 3 && repaired.All(n => n.Duration == 400), "load repairs old paste remnants without mutating source");
            CheckPastedShred(repaired);
            check(true, "repaired old copied chord is playable");
        }
        Console.WriteLine($"PASS v61: {count} recovery and legacy paste checks");
    }

    public static void CheckPastedShred(IReadOnlyList<ShredHitObject> source)
    {
        using var game = new TestSession();
        var notes = source.Select(n => game.Add(n.StartTime, n.Row, n.Fret, n.Articulation, n.Duration)).ToArray();
        var starts = source.Where(n => n.Row == 0).Select(n => n.StartTime).Order().ToArray();
        game.Seek(starts[0] - 100); game.Press(100); game.Press(109); game.Press(110);
        game.Seek(starts[0] + 1); game.Press(105); game.Press(114); game.Press(115);
        for (int i = 1; i < starts.Length; i++)
        {
            game.Seek(starts[i] + 1);
            if (i % 2 == 1) { game.Release(114); game.Release(115); game.FlushPicks(); }
            else { game.Press(114); game.Press(115); }
        }
        if (notes.Any(n => !n.HeadHit)) throw new Exception("Pasted pattern cannot be shredded: " + string.Join(",", notes.Where(n => !n.HeadHit).Select(n => n.HitObject.StartTime)));
    }
}
