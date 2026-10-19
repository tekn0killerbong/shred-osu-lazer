using System.Text;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Beatmaps;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;

internal static class Revision61SnapChecks
{
	private static int count;
	private static void check(bool value, string label)
	{
		if (!value) throw new Exception(label);
		count++;
	}
	private static string positions(IBeatmap map) => string.Join(";", map.HitObjects.Cast<ShredHitObject>()
		.OrderBy(n => n.StartTime).ThenBy(n => n.Row).ThenBy(n => n.Fret)
		.Select(n => $"{n.StartTime:R}/{n.EndTime:R}/{n.Row}/{n.Fret}"));
	public static void Run()
	{
		var ruleset = new ShredRuleset();
		using var store = new Store(ruleset.RulesetInfo);
		Decoder.RegisterDependencies(store);
		foreach (double bpm in new[] { 137d, 172, 333 })
		foreach (double origin in new[] { 0d, 123.4567 })
		foreach (int divisor in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 12, 16, 24, 32, 48, 64 })
		{
			var raw = new Beatmap<ShredHitObject>();
			raw.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
			raw.ControlPointInfo.Add(origin, new TimingControlPoint { BeatLength = 60000 / bpm });
			double start = ShredGridTiming.Snap(raw.ControlPointInfo, divisor, origin + 8 * 60000 / bpm + 60000 / bpm / divisor);
			double end = ShredGridTiming.Snap(raw.ControlPointInfo, divisor, start + 3 * 60000 / bpm / divisor);
			foreach (double offset in new[] { 0d, -.95, -.8 })
				raw.HitObjects.Add(new ShredHitObject { StartTime = start + offset, Duration = end - start, Fret = raw.HitObjects.Count + 1 });
			raw.HitObjects.Add(new ShredHitObject { StartTime = end - .95, Row = 1, Fret = 1 });
			var map = new FlatWorkingBeatmap(raw).GetPlayableBeatmap(ruleset.RulesetInfo);
			var notes = map.HitObjects.Cast<ShredHitObject>().ToArray();
			check(notes.Take(3).All(n => n.StartTime == start && n.EndTime == end), $"exact head/tail grid positions {bpm}/{origin}/{divisor}");
			check(notes[3].StartTime == end && notes[0].EndTime == notes[3].StartTime, $"tail and following head exactly equal {bpm}/{origin}/{divisor}: expected {end:R}, got {notes[3].StartTime:R}, all {positions(map)}");
			check(raw.HitObjects[1].StartTime == start - .95, "loading leaves original map unchanged");
			check(ShredGridTicks.Enumerate(map.ControlPointInfo, divisor, start - .001, end + .001).First().Time == start, "visible grid and snapped notes exactly equal");
			using var editor = new EditorBeatmap(map);
			string before = positions(editor);
			for (int repeat = 0; repeat < 5; repeat++)
			{
				using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
				new LegacyBeatmapEncoder(editor, null, null).Encode(writer);
				using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(writer.ToString()));
				using var reader = new LineBufferedReader(bytes);
				var decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
				var reopened = new FlatWorkingBeatmap(decoded).GetPlayableBeatmap(ruleset.RulesetInfo);
				check(positions(reopened) == before, "export/import preserves exact head and endpoint values");
				using var again = new EditorBeatmap(reopened);
				check(positions(again) == before, "reopening editor is idempotent");
			}
		}

		var mixed = new Beatmap<ShredHitObject>();
		mixed.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
		mixed.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 60000d / 172 });
		foreach (int divisor in new[] { 4, 6, 12, 16 })
		{
			double start = ShredGridTiming.Snap(mixed.ControlPointInfo, divisor, 8 * 60000d / 172 + 60000d / 172 / divisor);
			mixed.HitObjects.Add(new ShredHitObject { StartTime = start, Duration = ShredGridTiming.Snap(mixed.ControlPointInfo, divisor, start + 60000d / 172) - start, Fret = mixed.HitObjects.Count + 1 });
		}
		using (var editor = new EditorBeatmap(mixed))
		{
			using var history = new BeatmapEditorChangeHandler(editor);
			string original = positions(editor);
			_ = history.CurrentStateHash;
			double delta = ShredGridTiming.Snap(editor.ControlPointInfo, 4, 60000d / 172 / 4);
			var before = editor.HitObjects.Cast<ShredHitObject>().ToDictionary(n => n.Fret, n => n.StartTime);
			editor.BeginChange();
			foreach (var n in editor.HitObjects.Cast<ShredHitObject>().ToArray()) { n.StartTime += delta; editor.Update(n); }
			editor.EndChange();
			check(editor.HitObjects.Cast<ShredHitObject>().All(n => Math.Abs(n.StartTime - before[n.Fret] - delta) < .000002), "mixed subdivisions keep shared displacement, not current spacing");
			string moved = positions(editor);
			history.RestoreState(-1); check(positions(editor) == original, "undo restores exact positions");
			history.RestoreState(1); check(positions(editor) == moved, "redo restores exact positions");
			double old = editor.HitObjects[0].StartTime;
			editor.ControlPointInfo.TimingPoints[0].BeatLength += .001;
			editor.Update(editor.HitObjects[0]);
			check(editor.HitObjects[0].StartTime == old, "timing-only edits do not resnap unchanged notes");
		}
		Console.WriteLine($"PASS v61 snap: {count} exact timestamp, grid, export/import and undo checks");
	}
}
