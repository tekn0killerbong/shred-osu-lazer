using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Shred;
using osu.Game.Screens.Edit;
using System.Text;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;
using osu.Game.Rulesets.Shred.Beatmaps;
using osu.Game.Rulesets.Shred.Objects;

internal static class Revision68SnapChecks
{
	public static void Run()
	{
		int checks = 0;
		void check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
		foreach (double sourceBpm in new[] { 137d, 172, 333 })
		foreach (double targetBpm in new[] { 139d, 173, 287 })
		foreach (int divisor in new[] { 4, 6, 8, 12, 16, 24, 32, 48, 64 })
		{
			var src = new ControlPointInfo(); src.Add(123.4567, new TimingControlPoint { BeatLength = 60000 / sourceBpm });
			var dst = new ControlPointInfo(); dst.Add(4097.6789, new TimingControlPoint { BeatLength = 60000 / targetBpm });
			double start = ShredGridTiming.Snap(src, divisor, 1000);
			double tail = ShredGridTiming.Snap(src, divisor, start + 60000 / sourceBpm * 7 / divisor);
			var notes = new[] {
				new ShredHitObject { StartTime = start, Fret = 1, Duration = tail - start, HeadSnapDivisor = divisor, TailSnapDivisor = divisor },
				new ShredHitObject { StartTime = tail, Fret = 2, HeadSnapDivisor = divisor }
			};
			double target = ShredGridTiming.Snap(dst, divisor, 4500);
			var source = new ShredBeatTimeline(src);
			check(ShredGroupTiming.Apply(notes.Select(n => (n, source.Capture(n))), target, new ShredBeatTimeline(dst)), "apply fractional BPM group");
			foreach (var n in notes)
			{
				check(n.StartTime == ShredGridTiming.Snap(dst, divisor, n.StartTime), $"exact head snap {sourceBpm}/{targetBpm}/1:{divisor}: {n.StartTime:R}");
				check(n.EndTime == ShredGridTiming.Snap(dst, divisor, n.EndTime), "exact tail snap");
			}
			check(notes[0].EndTime == notes[1].StartTime, "shared LN endpoint stays identical");
		}
		var sourceTiming = new ControlPointInfo(); sourceTiming.Add(0, new TimingControlPoint { BeatLength = 500 });
		var destination = new ControlPointInfo(); destination.Add(0, new TimingControlPoint { BeatLength = 720 }); destination.Add(5003.5, new TimingControlPoint { BeatLength = 360 });
		var group = new[] { new ShredHitObject { StartTime = 1000, Fret = 1, HeadSnapDivisor = 4 }, new ShredHitObject { StartTime = 1750, Fret = 2, Duration = 500, HeadSnapDivisor = 4, TailSnapDivisor = 4 } };
		var origin = new ShredBeatTimeline(sourceTiming);
		ShredGroupTiming.Apply(group.Select(n => (n, origin.Capture(n))), 4500, new ShredBeatTimeline(destination));
		check(group.All(n => n.StartTime == ShredGridTiming.Snap(destination, 4, n.StartTime) && n.EndTime == ShredGridTiming.Snap(destination, 4, n.EndTime)), "tempo offset reset uses local grid for both endpoints");
		var ruleset = new ShredRuleset(); using var store = new Store(ruleset.RulesetInfo); Decoder.RegisterDependencies(store);
		var mixed = new Beatmap<ShredHitObject>(); mixed.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
		mixed.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
		mixed.HitObjects.Add(new ShredHitObject { StartTime = 1000, Fret = 1, HeadSnapDivisor = 4 });
		mixed.HitObjects.Add(new ShredHitObject { StartTime = ShredGridTiming.Snap(mixed.ControlPointInfo, 6, 1083.333333), Fret = 2,
			Duration = ShredGridTiming.Snap(mixed.ControlPointInfo, 6, 1166.666666) - ShredGridTiming.Snap(mixed.ControlPointInfo, 6, 1083.333333), HeadSnapDivisor = 6, TailSnapDivisor = 6 });
		var mixedTiming = new ShredBeatTimeline(mixed.ControlPointInfo);
		double destinationAnchor = ShredGridTiming.Snap(mixed.ControlPointInfo, 64, 2000 + 500d / 64);
		check(ShredGroupTiming.Apply(mixed.HitObjects.Select(n => (n, mixedTiming.Capture(n))), destinationAnchor, mixedTiming), "mixed fine-grid paste");
		check(mixed.HitObjects[1].HeadSnapDivisor == 192 && mixed.HitObjects[1].TailSnapDivisor == 192, "mixed 1/6 and 1/64 retain compound provenance");
		string signature(IBeatmap b) => string.Join(';', b.HitObjects.Cast<ShredHitObject>().Select(n => $"{n.StartTime:R}/{n.EndTime:R}/{n.HeadSnapDivisor}/{n.TailSnapDivisor}"));
		using (var editor = new EditorBeatmap(mixed))
		{
			string original = signature(editor);
			using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
			new LegacyBeatmapEncoder(editor, null, null).Encode(writer);
			using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(writer.ToString())); using var reader = new LineBufferedReader(bytes);
			var decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
			var reopened = new FlatWorkingBeatmap(decoded).GetPlayableBeatmap(ruleset.RulesetInfo);
			check(signature(reopened) == original, "compound snap metadata and exact times survive export/import");
			var recaptured = new ShredBeatTimeline(reopened.ControlPointInfo);
			var shifted = reopened.HitObjects.Cast<ShredHitObject>().ToArray();
			check(ShredGroupTiming.Apply(shifted.Select(n => (n, recaptured.Capture(n))), destinationAnchor + 500, recaptured), "compound grid can be moved again after reopening");
			check(Math.Abs(shifted[1].StartTime - shifted[0].StartTime - 500d / 6) < .000002, "triplet interval stays unchanged after a fine-grid move and reload");
		}
		Console.WriteLine($"PASS v68: {checks} exact-grid checks");
	}
}
