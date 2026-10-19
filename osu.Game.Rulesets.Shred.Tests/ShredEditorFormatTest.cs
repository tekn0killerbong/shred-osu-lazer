using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;
namespace osu.Game.Rulesets.Shred.Tests
{
	[TestFixture]
	[NonParallelizable]
	public class ShredEditorFormatTest
	{
		private TestRulesetStore store;
		[SetUp] public void SetUp() { store = new TestRulesetStore(); Decoder.RegisterDependencies(store); }
		[TearDown] public void TearDown() => store.Dispose();
		private sealed class TestRulesetStore : RulesetStore
		{
			public override IEnumerable<RulesetInfo> AvailableRulesets => new[]
			{
				new RulesetInfo("mania", "mania", typeof(ShredRuleset).AssemblyQualifiedName, 3) { Available = true },
				new ShredRuleset().RulesetInfo
			};
		}
		private static Beatmap<ShredHitObject> create()
		{
			Beatmap<ShredHitObject> map = new Beatmap<ShredHitObject>();
			map.BeatmapInfo.Ruleset = new ShredRuleset().RulesetInfo;
			map.Difficulty.CircleSize = 12;
			map.Metadata.Title = "Shred test";
			map.Metadata.Artist = "Test";
			map.Metadata.AudioFile = "song.ogg";
			map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
			map.ControlPointInfo.Add(4000, new TimingControlPoint { BeatLength = 400 });
			map.HitObjects.Add(new ShredHitObject { StartTime = 1000, Row = 0, Fret = 1 });
			map.HitObjects.Add(new ShredHitObject { StartTime = 1500, Row = 0, Fret = 3, Articulation = ShredPracticeArticulation.HammerOn });
			map.HitObjects.Add(new ShredHitObject { StartTime = 1500, Row = 1, Fret = 2, Articulation = ShredPracticeArticulation.PullOff });
			map.HitObjects.Add(new ShredHitObject { StartTime = 2000, Row = 0, Fret = 0, Articulation = ShredPracticeArticulation.PullOff });
			return map;
		}
		private static string encode(IBeatmap map)
		{
			using StringWriter writer = new StringWriter();
			new LegacyBeatmapEncoder(map, null, null).Encode(writer);
			return writer.ToString();
		}
		private static Beatmap decode(string text)
		{
			using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
			using LineBufferedReader reader = new LineBufferedReader(stream);
			return Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
		}
		[Test] public void NativeRoundTripKeepsAudioTimingAndBothStrings()
		{
			Beatmap<ShredHitObject> source = create();
			Beatmap decoded = decode(encode(source));
			Assert.That(decoded.BeatmapInfo.Ruleset.ShortName, Is.EqualTo("shred"));
			Assert.That(decoded.Metadata.AudioFile, Is.EqualTo("song.ogg"));
			Assert.That(decoded.Metadata.Title, Is.EqualTo("Shred test"));
			Assert.That(decoded.ControlPointInfo.TimingPointAt(5000).BeatLength, Is.EqualTo(400));
			Assert.That(decoded.HitObjects.Cast<ShredHitObject>().Select(n => (n.StartTime,n.Row,n.Fret,n.Articulation)), Is.EqualTo(source.HitObjects.Select(n => (n.StartTime,n.Row,n.Fret,n.Articulation))));
		}
		[Test] public void UnsortedObjectsKeepTheirArticulations()
		{
			Beatmap<ShredHitObject> source = create();
			source.HitObjects.Reverse();
			Beatmap decoded = decode(encode(source));
			Assert.That(decoded.HitObjects.Cast<ShredHitObject>().Select(n => (n.StartTime,n.Row,n.Fret,n.Articulation)), Is.EqualTo(source.HitObjects.OrderBy(n=>n.StartTime).Select(n => (n.StartTime,n.Row,n.Fret,n.Articulation))));
		}
		[Test] public void UndoRedoRestoresArticulationOnlyChanges()
		{
			Beatmap<ShredHitObject> map = create();
			using EditorBeatmap editor = new EditorBeatmap(map);
			byte[] before = Encoding.UTF8.GetBytes(encode(editor));
			((ShredHitObject)editor.HitObjects[1]).Articulation = ShredPracticeArticulation.PullOff;
			byte[] after = Encoding.UTF8.GetBytes(encode(editor));
			LegacyEditorBeatmapPatcher patcher = new LegacyEditorBeatmapPatcher(editor);
			patcher.Patch(after,before);
			Assert.That(((ShredHitObject)editor.HitObjects[1]).Articulation, Is.EqualTo(ShredPracticeArticulation.HammerOn));
			patcher.Patch(before,after);
			Assert.That(((ShredHitObject)editor.HitObjects[1]).Articulation, Is.EqualTo(ShredPracticeArticulation.PullOff));
		}
		[Test] public void EmptyNewMapGetsTwelveColumns()
		{
			Beatmap<ShredHitObject> map = create(); map.HitObjects.Clear(); map.Difficulty.CircleSize = 5;
			Assert.That(decode(encode(map)).Difficulty.CircleSize, Is.EqualTo(12));
		}
		[TestCase("Version:1", "Version:2")]
		[TestCase("1,1", "1,9")]
		[TestCase("CustomRuleset: shred", "CustomRuleset: missing-ruleset")]
		public void InvalidCustomDataIsRejected(string from, string to) => Assert.Throws<FormatException>(() => decode(encode(create()).Replace(from,to)));
		[Test] public void OrdinaryManiaDoesNotBecomeShred()
		{
			string text = encode(create());
			text = text.Substring(0,text.IndexOf("[RulesetData]",StringComparison.Ordinal)).Replace("CustomRuleset: shred", "");
			Beatmap map = decode(text);
			Assert.That(map.BeatmapInfo.Ruleset.ShortName, Is.EqualTo("mania"));
			Assert.That(map.HitObjects.Any(n=>n is ShredHitObject), Is.False);
		}
	}
}
