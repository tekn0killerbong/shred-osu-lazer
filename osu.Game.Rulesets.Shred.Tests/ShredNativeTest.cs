using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Shred.Beatmaps;
using osu.Game.Rulesets.Shred.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Framework.IO.Stores;
using osu.Game.Tests.Beatmaps;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Shred.Objects;
namespace osu.Game.Rulesets.Shred.Tests
{
	[TestFixture]
	public class ShredNativeTest
	{
		private const string header = "osu file format v14\n[General]\nMode:3\n[Difficulty]\nCircleSize:12\n[HitObjects]\n";
		[Test]
		public void EmbeddedMapAndTrackExist()
		{
			using Stream stream = typeof(ShredRuleset).Assembly.GetManifestResourceStream("osu.Game.Rulesets.Shred.Resources.shred-demo.osu");
			Assert.That(stream, Is.Not.Null);
			using StreamReader reader = new StreamReader(stream);
			var beatmap = ShredOsuReader.Read(reader, new ShredRuleset().RulesetInfo);
			Assert.That(beatmap.HitObjects.Count, Is.EqualTo(26));
			Assert.That(beatmap.HitObjects.Any(h => h.Articulation == ShredPracticeArticulation.HammerOn), Is.True);
			Assert.That(beatmap.HitObjects.Any(h => h.Articulation == ShredPracticeArticulation.PullOff), Is.True);
			Assert.That(beatmap.HitObjects.All(h => h.Row == 0 || h.Row == 1), Is.True);
			using var store = new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(ShredRuleset).Assembly), "Resources");
			byte[] audio = store.Get("shred-demo.wav");
			Assert.That(audio, Is.Not.Null);
			Assert.That(audio.Length, Is.GreaterThan(1000000));
		}
		[Test]
		public void DemoPassesNativePlayableBeatmapPipeline()
		{
			using Stream stream = typeof(ShredRuleset).Assembly.GetManifestResourceStream("osu.Game.Rulesets.Shred.Resources.shred-demo.osu");
			using StreamReader reader = new StreamReader(stream);
			ShredRuleset ruleset = new ShredRuleset();
			var beatmap = ShredOsuReader.Read(reader, ruleset.RulesetInfo);
			TestWorkingBeatmap working = new TestWorkingBeatmap(beatmap);
			var playable = (Beatmap<ShredHitObject>)working.GetPlayableBeatmap(ruleset.RulesetInfo);
			Assert.That(playable.HitObjects.Count, Is.EqualTo(26));
			Assert.That(playable.HitObjects.All(h => h.HitWindows is ShredHitWindows), Is.True);
			Assert.That(playable.HitObjects[0].HitWindows.WindowFor(HitResult.Perfect), Is.EqualTo(19.5));
		}

		[Test]
		public void TwelveColumnsPreserveOpenAndFretMeaning()
		{
			string data = header;
			for (int column = 0; column < 12; column++)
				data += $"{(int)((column + 0.5) * 512 / 12)},192,{column * 1000},1,0,0:0:0:0:\n";
			using StringReader reader = new StringReader(data);
			var beatmap = ShredOsuReader.Read(reader, new ShredRuleset().RulesetInfo);
			for (int column = 0; column < 12; column++)
			{
				Assert.That(beatmap.HitObjects[column].Row, Is.EqualTo(column / 6));
				Assert.That(beatmap.HitObjects[column].Fret, Is.EqualTo(column % 6));
			}
		}
		[Test]
		public void ArticulationsFollowOriginalIndexBeforeSorting()
		{
			using StringReader reader = new StringReader(header + "64,0,2000,1,0,0:0:0:0:\n21,0,1000,1,0,0:0:0:0:\n[ShredArticulations]\n0,P\n");
			var beatmap = ShredOsuReader.Read(reader, new ShredRuleset().RulesetInfo);
			Assert.That(beatmap.HitObjects[1].Articulation, Is.EqualTo(ShredPracticeArticulation.PullOff));
		}
		[TestCase(2)]
		[TestCase(8)]
		[TestCase(128)]
		public void UnsupportedObjectTypesAreRejected(int type)
		{
			using StringReader reader = new StringReader(header + $"64,0,1000,{type},0,2000:0:0:0:0:\n");
			Assert.Throws<FormatException>(() => ShredOsuReader.Read(reader, new ShredRuleset().RulesetInfo));
		}
		[Test]
		public void ShredOdFiveHasGreatAndOuterMissWindows()
		{
			ShredHitWindows windows = new ShredHitWindows();
			windows.SetDifficulty(5);
            Assert.That(windows.WindowFor(HitResult.Great), Is.EqualTo(100));
            Assert.That(windows.WindowFor(HitResult.Good), Is.EqualTo(100));
            Assert.That(windows.WindowFor(HitResult.Miss), Is.EqualTo(250));
            Assert.That(windows.ResultFor(-100), Is.EqualTo(HitResult.Great));
            Assert.That(windows.ResultFor(100.01), Is.EqualTo(HitResult.Miss));
            Assert.That(windows.ResultFor(250.01), Is.EqualTo(HitResult.None));
		}
	}
}
