using System.Diagnostics;
using osu.Framework.Audio.Sample;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Compatibility;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Skinning;

internal static class Revision47Checks
{
	public static string CreateUniversalSkin()
	{
		string path=Path.Combine(Path.GetTempPath(), "shred-universal-"+Guid.NewGuid()+".osk");
		using var zip=System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create);
		using(var writer=new StreamWriter(zip.CreateEntry("skin.ini").Open()))
			writer.Write("[General]\nName: Shred universal sample probe\nAuthor: Shred regression tests\nVersion: 2.7\n");
		using var resources=new ShredRuleset().CreateResourceStore();
		using(var audio=zip.CreateEntry("hitnormal.wav").Open()) audio.Write(resources.Get("Samples/Gameplay/drum-hitnormal.wav"));
		return path;
	}
	public static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS v47 " + label); }
	public static void Run()
	{
		var notes = Enumerable.Range(0, 1000).Select(i => new ShredHitObject { StartTime=i*125, Fret=1+i%5, Row=i%2, Duration=500,
			Samples=new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, "soft") } }).ToArray();
		foreach (var n in notes) ShredEditorCodec.Synchronise(n);
		foreach (var n in notes) ShredEditorCodec.Synchronise(n);
		long start = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew();
		for (int i=0;i<100;i++) foreach (var n in notes) ShredEditorCodec.Synchronise(n);
		timer.Stop(); long bytes = GC.GetAllocatedBytesForCurrentThread()-start;
		Console.WriteLine($"PERF unchanged envelopes: 100 x 1000 notes, {timer.Elapsed.TotalMilliseconds:F2} ms, {bytes} bytes");
		Check(bytes < 100000, "unchanged map preprocessing avoids repeated JSON allocations");
		var samples = ShredEditorCodec.PlayableSamples(notes[0].Samples); samples.Clear();
		Check(ShredEditorCodec.PlayableSamples(notes[0].Samples).Count == 1, "editable sample lists do not mutate cached payload");
		notes[0].Duration=750; notes[0].Row=1; notes[0].Fret=5; ShredEditorCodec.Synchronise(notes[0]);
		var carrier = new osu.Game.Rulesets.Osu.Objects.HitCircle { Samples=notes[0].Samples.ToList() };
		Check(ShredEditorCodec.TryDecode(carrier, out var changed) && changed.Duration==750 && changed.Row==1 && changed.Fret==5, "cached envelope updates after geometry change");
		notes[0].Samples = new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, "drum", volume: 35) };
		ShredEditorCodec.Synchronise(notes[0]);
		Check(ShredEditorCodec.PlayableSamples(notes[0].Samples).Single().Bank=="drum" && ShredEditorCodec.PlayableSamples(notes[0].Samples).Single().Volume==35, "new bank and volume replace cached payload");
		using var skin = new GenericLegacySkin();
		var transformed = new ShredRuleset().CreateSkinTransformer(skin, new Beatmap()) ?? skin;
		foreach (string bank in HitSampleInfo.ALL_BANKS)
			Check(transformed.GetSample(new HitSampleInfo(HitSampleInfo.HIT_NORMAL, bank))==null, bank+" ignores generic user-skin fallback so bundled bank can load");
		skin.SkinInfo.Value.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash="bank" }, "soft-hitnormal.wav"));
		Check(transformed.GetSample(new HitSampleInfo(HitSampleInfo.HIT_NORMAL,"soft")) == null, "bank-specific global skin file yields to Shred guitars");
		Check(ReferenceEquals(transformed.GetSample(new osu.Game.Rulesets.Objects.Legacy.ConvertHitObjectParser.FileHitSampleInfo("custom.wav",50)),skin.Sound), "explicit custom filename keeps native routing");
	}
	private sealed class GenericLegacySkin : LegacySkin
	{
		public readonly ISample Sound = new SampleVirtual();
		public GenericLegacySkin() : base(new SkinInfo("generic", "test"), null)
		{
			SkinInfo.Value.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash="generic" }, "hitnormal.wav"));
		}
		public override ISample GetSample(ISampleInfo info) => Sound;
	}
}
