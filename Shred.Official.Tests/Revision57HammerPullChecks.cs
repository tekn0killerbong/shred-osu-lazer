using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects.Drawables;

internal static partial class GameplayChecks
{
    // v58 replaces the v57 recovery requirement: no initial or post-miss pick gate.
    public static void RunRevision58HammerPulls()
    {
        int cases = 0;
        foreach (int row in new[] { 0, 1 })
        foreach (int count in new[] { 1, 2, 3, 4, 5 })
        foreach (bool reverse in new[] { false, true })
        foreach (double offset in new[] { -75d, 0, 75d })
        foreach (double duration in new[] { 0d, 200d })
        foreach (string start in new[] { "first", "miss", "other row miss" })
        {
            using TestSession game = new TestSession();
            int key = 100 + row * 9;
            if (start != "first")
            {
                var missed = game.Add(300, start == "miss" ? row : 1 - row, 5, ShredPracticeArticulation.HammerPull);
                game.Seek(800); missed.CheckExpiry();
                if (missed.Result.Type != HitResult.Miss) throw new Exception("Expected a real preceding miss");
            }
            TestNote[] chord = Enumerable.Range(1, count)
                .Select(fret => game.Add(1000, row, fret, ShredPracticeArticulation.HammerPull, duration)).ToArray();
            TestNote skipped = game.Add(1300, row, 5, ShredPracticeArticulation.HammerPull);
            TestNote next = game.Add(1700, row, 1, ShredPracticeArticulation.HammerPull);
            game.Seek(1000 + offset);
            IEnumerable<int> frets = Enumerable.Range(0, count);
            if (reverse) frets = frets.Reverse();
            foreach (int fret in frets) game.Press(key + fret);
            if (chord.Any(note => !note.HeadHit || hammerPullHeadResult(note) != HitResult.Great))
                throw new Exception($"HPs must work without any pick: row={row}, count={count}, reverse={reverse}, offset={offset}, duration={duration}, start={start}");
            game.Seek(1250);
            foreach (int fret in frets) game.Release(key + fret);
            game.Seek(1700); game.Press(key);
            if (!next.HeadHit || skipped.Result.Type != HitResult.Miss) throw new Exception("A subsequent miss must not block another HP");
            cases++;
        }
        foreach (int row in new[] { 0, 1 })
        {
            int key = 100 + row * 9;
            foreach (int fret in new[] { 0, 1, 3 })
            {
                using TestSession game = new TestSession();
                var target = game.Add(1000, row, fret, ShredPracticeArticulation.HammerPull);
                game.Seek(500);
                if (fret > 0) game.Press(key + fret - 1);
                game.Press(key + 4);
                game.Seek(1000); game.Release(key + 4);
                if (!target.HeadHit) throw new Exception("Initial open/fretted pull-off must not require a pick");
                cases++;
            }
            foreach (bool pickFirst in new[] { false, true })
            {
                using TestSession game = new TestSession();
                var hp = new[] { 1, 2 }.Select(f => game.Add(1000, row, f, ShredPracticeArticulation.HammerPull)).ToArray();
                var ordinary = new[] { 3, 4 }.Select(f => game.Add(1000, row, f)).ToArray();
                game.Seek(1000);
                if (pickFirst) game.Press(key + 5);
                game.Press(key); game.Press(key + 1);
                game.Press(key + 2); game.Press(key + 3);
                if (hp.Concat(ordinary).Any(n => n.HeadJudged)) throw new Exception("Mixed chord must wait for all regular picks");
                game.Press(key + (pickFirst ? 6 : 5));
                if (pickFirst && hp.Concat(ordinary).Any(n => !n.HeadHit)) throw new Exception("Two regular picks must complete mixed chord");
                if (!pickFirst && hp.Concat(ordinary).Any(n => n.HeadJudged)) throw new Exception("One regular pick cannot partially complete mixed chord");
                game.Press(key + (pickFirst ? 7 : 6));
                if (ordinary.Any(n => !n.HeadHit)) throw new Exception("Free HP members must not consume ordinary pick capacity");
                cases++;
            }
            using (TestSession game = new TestSession())
            {
                var ordinary = new[] { 3, 4 }.Select(f => game.Add(1000, row, f)).ToArray();
                var hp = new[] { 1, 2 }.Select(f => game.Add(1000, row, f, ShredPracticeArticulation.HammerPull)).ToArray();
                game.Seek(1000); game.Press(key + 2); game.Press(key + 3); game.Press(key + 5);
                game.Press(key); game.Press(key + 1);
                if (hp.Concat(ordinary).Any(n => n.HeadJudged)) throw new Exception("HPs remain staged while mixed chord waits for second pick");
                game.Press(key + 6);
                if (ordinary.Any(n => !n.HeadHit)) throw new Exception("Mixed armed chord lost its second regular pick");
                cases++;
            }
            using (TestSession game = new TestSession())
            {
                var target = game.Add(1000, row, 1, ShredPracticeArticulation.HammerPull);
                int other = 100 + (1 - row) * 9;
                game.Seek(500); game.Press(key); game.Press(other + 3);
                game.Seek(1000); game.Release(other + 3);
                if (target.HeadJudged) throw new Exception("Pull-offs must remain row-local");
                game.Release(key); game.Press(key);
                if (!target.HeadHit) throw new Exception("Own-row fret gesture must still hit");
                cases++;
            }
            foreach (string guard in new[] { "early", "late", "paused", "interior", "held" })
            {
                using TestSession game = new TestSession();
                var chord = new[] { 1, 3 }.Select(f => game.Add(1000, row, f, ShredPracticeArticulation.HammerPull)).ToArray();
                game.Seek(guard == "late" ? 1190 : guard == "early" || guard == "held" ? 800 : 1000);
                if (guard == "paused") game.Ruleset.IsPaused.Value = true;
                if (guard == "interior") game.Press(key + 1);
                game.Press(key); game.Press(key + 2);
                if (guard == "held") { game.Seek(1000); game.Press(key + 5 - 9 * row + 9 * (1 - row)); }
                if (chord.Any(n => n.HeadHit)) throw new Exception("Removing pick gating bypassed " + guard);
                cases++;
            }
        }
        foreach (int row in new[] { 0, 1 })
        foreach (bool open in new[] { false, true })
        {
            using TestSession game = new TestSession();
            var notes = new[]
            {
                game.Add(1000, row, open ? 0 : 1, ShredPracticeArticulation.HammerPull),
                game.Add(1500, row, 2, ShredPracticeArticulation.HammerPull, 200),
                game.Add(2000, row, 1, ShredPracticeArticulation.HammerPull),
                game.Add(2000, row, 3), game.Add(2000, row, 4),
            };
            var map = new osu.Game.Beatmaps.Beatmap<osu.Game.Rulesets.Shred.Objects.ShredHitObject>();
            map.HitObjects.AddRange(notes.Select(n => n.HitObject));
            var frames = new osu.Game.Rulesets.Shred.Replays.ShredAutoGenerator(map).Generate().Frames
                .OfType<osu.Game.Rulesets.Shred.Replays.ShredReplayFrame>().ToArray();
            bool pick(osu.Game.Rulesets.Shred.ShredAction action) => (int)action >= 100 && (int)action <= 117 && ((int)action - 100) % 9 >= 5;
            if (frames.Any(f => f.Time < 1900 && f.Actions.Any(pick))) throw new Exception("Autoplay unnecessarily picked an initial HP");
            var previous = new HashSet<osu.Game.Rulesets.Shred.ShredAction>();
            int pickEdges = 0;
            foreach (var frame in frames)
            {
                game.Seek(frame.Time);
                foreach (var action in previous.Except(frame.Actions).ToArray()) game.Release((int)action);
                foreach (var action in frame.Actions.Except(previous).ToArray()) { game.Press((int)action); if (pick(action)) pickEdges++; }
                previous = frame.Actions.ToHashSet();
            }
            if (notes.Any(n => !n.HeadHit) || notes.Where(n => n.HitObject.Articulation == ShredPracticeArticulation.HammerPull)
                .Any(n => hammerPullHeadResult(n) != HitResult.Great) || pickEdges != 2)
                throw new Exception("Autoplay must hit HPs freely and pick only the ordinary members");
            cases++;
        }
        Console.WriteLine($"PASS v58 {cases} HP scenarios: map starts, repeated misses, both rows, 1–5 notes, LN heads, pull-offs, mixed pick chords, autoplay and input guards");
    }

    private static HitResult hammerPullHeadResult(TestNote note)
    {
        if (!note.HitObject.IsLong) return note.Result.Type;
        var head = (DrawableShredHoldHead)typeof(DrawableShredHitObject).GetField("head", hidden).GetValue(note);
        return head.Result.Type;
    }
}
