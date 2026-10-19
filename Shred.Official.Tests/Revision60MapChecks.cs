using System.IO.Compression;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Replays;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;

internal static partial class GameplayChecks
{
    public static void RunRevision60Map(string archive)
    {
        var ruleset = new ShredRuleset();
        using var store = new Store(ruleset.RulesetInfo);
        Decoder.RegisterDependencies(store);
        using var zip = ZipFile.OpenRead(archive);
        using var stream = zip.Entries.Single(e => e.FullName.EndsWith(".osu")).Open();
        using var reader = new LineBufferedReader(stream);
        var decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
        var map = new FlatWorkingBeatmap(decoded).GetPlayableBeatmap(ruleset.RulesetInfo);
        using (var game = new TestSession())
        {
            var notes = map.HitObjects.Cast<ShredHitObject>().Select(n => game.Add(n.StartTime, n.Row, n.Fret, n.Articulation, n.Duration)).ToArray();
            var frames = new ShredAutoGenerator(map).Generate().Frames.OfType<ShredReplayFrame>();
            var previous = new HashSet<ShredAction>();
            foreach (var frame in frames)
            {
                game.Seek(frame.Time);
                foreach (var action in previous.Except(frame.Actions).ToArray()) game.Release((int)action);
                foreach (var action in frame.Actions.Except(previous).ToArray()) game.Press((int)action);
                previous = frame.Actions.ToHashSet();
            }
            var failed = notes.Where(n => !n.HeadHit).ToArray();
            if (failed.Length > 0) throw new Exception("Actual map autoplay missed: " + string.Join(", ", failed.Take(10).Select(n => n.HitObject.StartTime + ":" + n.HitObject.Column)));
            Console.WriteLine($"PASS actual map: all {notes.Length} note heads played through normal inputs and autoplay");
        }
        using (var game = new TestSession())
        {
            var times = map.HitObjects.Select(n => n.StartTime).Distinct().OrderBy(t => t).Take(8).ToArray();
            var expanded = times.Select(t => new[] { game.Add(t, 0, 3, duration: 174), game.Add(t, 1, 1, duration: 174), game.Add(t, 1, 2, duration: 174) }).ToArray();
            game.Seek(times[0] - 100); game.Press(102); game.Press(109); game.Press(110);
            game.Seek(times[0]); game.Press(105); game.Press(114); game.Press(115);
            for (int i = 1; i < times.Length; i++)
            {
                game.Seek(times[i]);
                if (i % 2 == 1) { game.Release(114); game.Release(115); game.FlushPicks(); }
                else { game.Press(114); game.Press(115); }
            }
            if (expanded.SelectMany(n => n).Any(n => !n.HeadHit)) throw new Exception("Expanded real-map 3-note LN chord pattern failed");
            Console.WriteLine("PASS actual map rhythm: 24 heads in eight full 3-note LN chords, same 2+1 picking gesture");
        }
    }
}
