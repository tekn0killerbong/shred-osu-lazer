using System.Reflection;
using System.Text;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Beatmaps;
using osu.Game.Rulesets.Shred.Objects;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;

Revision61ForkExportChecks.Run();

internal static class Revision61ForkExportChecks
{
	private sealed class Store : RulesetStore
	{
		public override IEnumerable<RulesetInfo> AvailableRulesets => new[] {
			new ShredRuleset().RulesetInfo,
			new RulesetInfo { ShortName = "mania", OnlineID = 3, Available = true, InstantiationInfo = new ShredRuleset().RulesetInfo.InstantiationInfo },
			new RulesetInfo { ShortName = "osu", OnlineID = 0, Available = true, InstantiationInfo = new ShredRuleset().RulesetInfo.InstantiationInfo }
		};
	}
	public static void Run()
	{
		using var store = new Store();
		Decoder.RegisterDependencies(store);
		var map = new Beatmap<ShredHitObject>();
		map.BeatmapInfo.Ruleset = new ShredRuleset().RulesetInfo;
		map.Difficulty.CircleSize = 12;
		map.ControlPointInfo.Add(123.4567, new TimingControlPoint { BeatLength = 60000d / 172 });
		double start = ShredGridTiming.Snap(map.ControlPointInfo, 6, 2000);
		double end = ShredGridTiming.Snap(map.ControlPointInfo, 12, 3000);
		map.HitObjects.Add(new ShredHitObject { StartTime = start, Duration = end - start, Fret = 1 });
		map.HitObjects.Add(new ShredHitObject { StartTime = start, Duration = end - start, Row = 1, Fret = 1 });
		map.HitObjects.Add(new ShredHitObject { StartTime = end, Row = 1, Fret = 2 });
		string folder = Path.Combine(Path.GetTempPath(), "shred-v61-export-" + Guid.NewGuid());
		var storage = new NativeStorage(folder);
		var exporter = new LegacyBeatmapExporter(storage);
		typeof(LegacyBeatmapExporter).GetMethod("MutateBeatmap", BindingFlags.Instance | BindingFlags.NonPublic)!
			.Invoke(exporter, new object[] { new BeatmapSetInfo(), map });
		if (map.HitObjects[0].StartTime != start || map.HitObjects[0].EndTime != end || map.ControlPointInfo.TimingPoints[0].Time != 123.4567)
			throw new Exception("Developer export rounded authored times");
		using var writer = new StringWriter();
		new LegacyBeatmapEncoder(map, null, null).Encode(writer);
		using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(writer.ToString()));
		using var reader = new LineBufferedReader(bytes);
		var decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
		var playable = new FlatWorkingBeatmap(decoded).GetPlayableBeatmap(new ShredRuleset().RulesetInfo);
		var notes = playable.HitObjects.Cast<ShredHitObject>().ToArray();
		if (notes.Length != 3 || notes.Take(2).Any(n => n.StartTime != start || n.EndTime != end) || notes[2].StartTime != end)
			throw new Exception("Developer export/import lost aligned heads or endpoints");
		Console.WriteLine("PASS v61 developer exporter and portable round-trip preserve exact heads, endpoints and timing points");
	}
}
