using System.IO.Compression;
using System.Reflection;
using System.Text;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.IO;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Compatibility;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components.TernaryButtons;
using osu.Game.Skinning;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;
using LegacySample = osu.Game.Rulesets.Objects.Legacy.ConvertHitObjectParser.LegacyHitSampleInfo;
using FileSample = osu.Game.Rulesets.Objects.Legacy.ConvertHitObjectParser.FileHitSampleInfo;

internal static class Revision49Checks
{
	private const BindingFlags private_fields = BindingFlags.Instance | BindingFlags.NonPublic;
	private static void Check(bool ok, string label)
	{
		if (!ok) throw new Exception(label);
		Console.WriteLine("PASS v49 " + label);
	}
	private static Beatmap Read(string text)
	{
		using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(text));
		using var reader = new LineBufferedReader(bytes);
		return Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
	}
	private static IBeatmap ReadArchive(string path)
	{
		using var zip = ZipFile.OpenRead(path);
		var entry = zip.Entries.Single(e => e.Name.EndsWith("[Riff].osu") || e.Name == "Jimi_Hendrix_Riff_audio_v49.osu");
		using var reader = new StreamReader(entry.Open());
		return new ShredRuleset().CreateBeatmapConverter(Read(reader.ReadToEnd())).Convert();
	}
	private static string Geometry(IBeatmap map) => string.Join("\n", map.HitObjects.Cast<ShredHitObject>().Select(n =>
		FormattableString.Invariant($"{n.StartTime:R}|{n.Duration:R}|{n.Row}|{n.Fret}|{n.Articulation}")));
	public static void Run(string original, string repaired)
	{
		var ruleset = new ShredRuleset();
		using var store = new Store(ruleset.RulesetInfo);
		Decoder.RegisterDependencies(store);
		var duplicates = Enumerable.Range(0, 16).Select(_ => (HitSampleInfo)new LegacySample("hitnormal", "normal", 100, true, 0, false)).ToList();
		Check(ShredEditorCodec.PlayableSamples(duplicates).Count == 1, "16 identical legacy samples become one");
		duplicates.Add(new LegacySample("hitnormal", "normal", 50, true, 0, false));
		duplicates.Add(new LegacySample("hitnormal", "soft", 100, true, 0, false));
		duplicates.Add(new LegacySample("hitnormal", "normal", 100, true, 2, false));
		duplicates.Add(new LegacySample("hitnormal", "normal", 100, true, 0, true));
		duplicates.Add(new LegacySample("hitclap", "normal", 100, true, 0, false));
		duplicates.Add(new FileSample("custom.wav", 100)); duplicates.Add(new FileSample("custom.wav", 100));
		Check(ShredEditorCodec.PlayableSamples(duplicates).Count == 8, "distinct volume, bank, index, layering, effect and explicit files preserved");
		using var beatmapSkin = new LegacyBeatmapSkin(new BeatmapInfo(), null);
		Check(ruleset.CreateSkinTransformer(beatmapSkin, new Beatmap()) == null, "authored beatmap sample overrides retain standard lookup");
		var before = ReadArchive(original);
		var after = ReadArchive(repaired);
		Check(before.HitObjects.Count == 366 && before.HitObjects.All(n => n.Samples.Count == 1), "actual corrupt Riff auto-recovers on conversion");
		Check(Geometry(before) == Geometry(after), "repaired file exactly preserves 366 times, durations, rows, frets and articulations");
		Check(after.HitObjects.All(n => n.Samples.Count == 1 && n.Samples[0].GetType() == typeof(HitSampleInfo)
			&& n.Samples[0].Bank == "normal" && n.Samples[0].Name == "hitnormal" && !n.Samples[0].UseBeatmapSamples),
			"repaired map requests one ordinary normal guitar from DLL, without map WAV dependency");
		string geometry = Geometry(after);
		for (int i = 0; i < 10; i++)
		{
			after.BeatmapInfo.Ruleset = after.BeatmapInfo.Ruleset.Clone();
			after.BeatmapInfo.Ruleset.OnlineID = 0;
			foreach (ShredHitObject note in after.HitObjects) ShredEditorCodec.Synchronise(note);
			using var writer = new StringWriter();
			new LegacyBeatmapEncoder(after, null, null).Encode(writer);
			after = ruleset.CreateBeatmapConverter(Read(writer.ToString())).Convert();
			Check(after.HitObjects.All(n => n.Samples.Count == 1) && Geometry(after) == geometry, "save/reimport cycle " + (i + 1) + " keeps one sample and unchanged mapping");
		}
	}

	public static string CreateConflictingSkin()
	{
		string path = Path.Combine(Path.GetTempPath(), "shred-v49-conflicting-" + Guid.NewGuid() + ".osk");
		using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
		using (var writer = new StreamWriter(zip.CreateEntry("skin.ini").Open()))
			writer.Write("[General]\nName: v49 conflicting global bank files\nAuthor: Shred tests\nVersion: 2.7\n");
		// A deliberately different 100 ms PCM sound in every global skin slot.
		using var bytes = new MemoryStream();
		using (var wave = new BinaryWriter(bytes, Encoding.UTF8, true))
		{
			wave.Write(Encoding.ASCII.GetBytes("RIFF")); wave.Write(36 + 8820);
			wave.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); wave.Write(16);
			wave.Write((short)1); wave.Write((short)1); wave.Write(44100); wave.Write(88200);
			wave.Write((short)2); wave.Write((short)16); wave.Write(Encoding.ASCII.GetBytes("data")); wave.Write(8820);
			wave.Write(new byte[8820]);
		}
		foreach (var file in Revision45Checks.Files)
		{
			using var sample = zip.CreateEntry(file.name + ".wav").Open();
			sample.Write(bytes.ToArray());
		}
		return path;
	}
	public static IEnumerable<Action> ImportSkin(SkinManager skins, string path)
	{
		var known = skins.GetAllUsableSkins().Select(s => s.PerformRead(i => i.ID)).ToHashSet();
		var import = skins.Import(new[] { new ImportTask(File.OpenRead(path), Path.GetFileName(path)) });
		while (!import.IsCompleted) yield return () => { };
		import.GetAwaiter().GetResult();
		while (!skins.GetAllUsableSkins().Any(s => s.PerformRead(i => !known.Contains(i.ID)))) yield return () => { };
		yield return () => skins.CurrentSkinInfo.Value = skins.GetAllUsableSkins().Single(s => s.PerformRead(i => !known.Contains(i.ID)));
		yield return () => { };
	}
	private static void CheckSounds(ShredHitObjectComposer composer, string bank, int count)
	{
		var drawables = composer.ChildrenOfType<DrawableShredEditorNote>().ToArray();
		Check(drawables.Length == 366, "366 editor notes loaded");
		foreach (var note in drawables)
		{
			var sounds = note.ChildrenOfType<PoolableSkinnableSample>().ToArray();
			if (sounds.Length != count) throw new Exception("Unexpected audio channel count: " + sounds.Length);
			foreach (var sound in sounds)
			{
				var info = (HitSampleInfo)typeof(PoolableSkinnableSample).GetField("sampleInfo", private_fields)!.GetValue(sound)!;
				var sample = (ISample)sound.Sample.GetType().GetField("sample", private_fields)!.GetValue(sound.Sample)!;
				string name = bank + "-" + info.Name;
				if (sample.Name != "Gameplay/" + name || !sample.IsLoaded
					|| Math.Abs(sample.Length - Revision45Checks.Files.Single(f => f.name == name).ms) > 2
					|| sound.AggregateFrequency.Value != 1 || Math.Abs(sound.AggregateVolume.Value - 1) > .000001)
					throw new Exception($"Wrong sample {sample.Name}/{sample.Length}, expected embedded guitar {name}");
			}
		}
		Console.WriteLine($"PASS v49 all 366 notes load {count} exact embedded guitar samples for {bank}, despite conflicting global skin");
	}
	public static IEnumerable<Action> Steps(Editor editor)
	{
		var composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
		var map = composer.Dependencies.Get<EditorBeatmap>();
		var clock = composer.Dependencies.Get<EditorClock>();
		string geometry = Geometry(map);
		yield return () => { clock.Stop(); composer.Playback.ReturnOnPause.Value = false; clock.Seek(0); };
		foreach (var step in ImportSkin(composer.Dependencies.Get<SkinManager>(), CreateConflictingSkin())) yield return step;
		yield return () => CheckSounds(composer, "normal", 1);
		foreach (double position in new[] { 5000.0, 80000.0 })
		{
			yield return () => { clock.Seek(position); clock.Start(); EditorUiProbe.FrameCosts.Clear(); EditorUiProbe.CaptureFrames = true; };
			for (int i = 0; i < 12; i++) yield return () => { };
			yield return () =>
			{
				EditorUiProbe.CaptureFrames = false; clock.Stop();
				var frames = EditorUiProbe.FrameCosts.Order().ToArray();
				Console.WriteLine($"PERF v49 Riff position={position} frames={frames.Length} p50={frames[frames.Length/2]:F3} p95={frames[(int)(frames.Length*.95)]:F3} max={frames.Last():F3}");
				var crossed = composer.ChildrenOfType<DrawableShredEditorNote>().Where(d => d.HitObject.StartTime > position && d.HitObject.StartTime < clock.CurrentTime).ToArray();
				Check(crossed.Length > 0 && crossed.All(d => d.ChildrenOfType<PoolableSkinnableSample>().Single().Played), "single guitar actually plays at position " + position);
			};
		}
		foreach (string bank in HitSampleInfo.ALL_BANKS)
		{
			yield return () =>
			{
				map.SelectedHitObjects.Clear(); map.SelectedHitObjects.AddRange(map.HitObjects);
				composer.ChildrenOfType<SampleBankTernaryButton>().Single(b => b.BankName == bank).NormalButton.TriggerClick();
				composer.ChildrenOfType<SampleBankTernaryButton>().Single(b => b.BankName == bank).AdditionsButton.TriggerClick();
				foreach (var pair in new[] { (HitSampleInfo.HIT_WHISTLE, GlobalAction.EditorToggleWhistleSound), (HitSampleInfo.HIT_FINISH, GlobalAction.EditorToggleFinishSound), (HitSampleInfo.HIT_CLAP, GlobalAction.EditorToggleClapSound) })
					if (composer.SelectionSampleStates[pair.Item1].Value != TernaryState.True)
						composer.ChildrenOfType<DrawableTernaryButton<GlobalAction>>().Single(b => b.Action == pair.Item2).TriggerClick();
				clock.Seek(map.HitObjects.First().StartTime - 200);
			};
			yield return () => { CheckSounds(composer, bank, 4); clock.Start(); };
			while (clock.CurrentTime < map.HitObjects.First().StartTime + 600) yield return () => { };
			yield return () =>
			{
				clock.Stop();
				var crossed = composer.ChildrenOfType<DrawableShredEditorNote>().Where(d => d.HitObject.StartTime < clock.CurrentTime).ToArray();
				Check(crossed.Length > 0 && crossed.All(d => d.ChildrenOfType<PoolableSkinnableSample>().Count(s => s.Played) == 4), "four supplied guitars actually play for " + bank);
			};
		}
		yield return () =>
		{
			Check(Geometry(map) == geometry, "all bank/effect edits preserve original mapping");
			using var writer = new StringWriter();
			new LegacyBeatmapEncoder(map, map.BeatmapSkin, map.Storyboard).Encode(writer);
			var reloaded = new ShredRuleset().CreateBeatmapConverter(Read(writer.ToString())).Convert();
			Check(Geometry(reloaded) == geometry && reloaded.HitObjects.All(n => n.Samples.Count == 4), "editor save/reload retains exactly four intended samples, without duplication");
		};
	}
}
