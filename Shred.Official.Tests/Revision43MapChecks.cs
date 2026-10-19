using System.IO.Compression;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Objects;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;

internal static partial class GameplayChecks
{
    public static void ProbeActualMap(string archive, bool requireSuccess)
    {
        var ruleset = new ShredRuleset();
        using var store = new Store(ruleset.RulesetInfo);
        Decoder.RegisterDependencies(store);
        using var zip = ZipFile.OpenRead(archive);
        using var stream = zip.Entries.Single(e=>e.FullName.EndsWith(".osu")).Open();
        using var reader = new LineBufferedReader(stream);
        var decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
        var map = new FlatWorkingBeatmap(decoded).GetPlayableBeatmap(ruleset.RulesetInfo);
        var source = map.HitObjects.OfType<ShredHitObject>().Where(n=>n.StartTime >= 8290 && n.StartTime < 9160).ToArray();
        if (source.Length != 4) throw new Exception("Expected two holds and the next two-note chord");
        foreach(var n in source) Console.WriteLine($"ACTUAL row={n.Row+1} fret={n.Fret} start={n.StartTime:R} end={n.EndTime:R}");
        int failures=0,cases=0;
        foreach(double late in new[]{0d,25,50,75,90})
        foreach(double release in new[]{-100d,0,100})
        foreach(bool reverse in new[]{false,true})
        {
            using var g=new TestSession();
            var notes=source.Select(n=>g.Add(n.StartTime,n.Row,n.Fret,n.Articulation,n.Duration)).ToArray();
            g.Seek(8250);g.Press(102);g.Press(103);
            g.Seek(8291);g.Press(105);g.Press(106);
            if(notes.Take(2).Any(n=>!n.HeadHit)) throw new Exception("Actual map setup failed");
            var events=new List<(double t,Action a)> {
                (8989+release,()=>g.Release(102)),(8994+release,()=>g.Release(103)),
                (8974+late,()=>g.Press(109+(reverse ? 1 : 0))),
                (8979+late,()=>g.Press(109+(reverse ? 0 : 1))),
                (8989+late,()=>g.Press(114)),(8994+late,()=>g.Press(115))
            };
            foreach(var e in events.OrderBy(e=>e.t)){g.Seek(e.t);e.a();}
            cases++;
            if(notes.Skip(2).Any(n=>!n.HeadHit)) {
                failures++;Console.WriteLine($"ACTUAL blocked late={late}, release={release}, reverse={reverse}; hits={string.Join(',',notes.Skip(2).Select(n=>n.HeadHit))}");
            }
        }
        Console.WriteLine($"ACTUAL MAP {cases-failures}/{cases} chords accepted");
        if(requireSuccess && failures>0) throw new Exception("Actual map chord regression");
    }
}
