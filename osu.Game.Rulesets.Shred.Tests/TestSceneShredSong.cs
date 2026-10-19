using System;
using System.IO;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Shred.Beatmaps;
using osu.Game.Storyboards;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;
namespace osu.Game.Rulesets.Shred.Tests
{
	public partial class TestSceneShredSong : PlayerTestScene
	{
		protected override Ruleset CreatePlayerRuleset() => new ShredRuleset();
		protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
		{
			using Stream stream = typeof(ShredRuleset).Assembly.GetManifestResourceStream("osu.Game.Rulesets.Shred.Resources.shred-demo.osu");
			using StreamReader reader = new StreamReader(stream ?? throw new InvalidOperationException("Missing embedded demo map."));
			return ShredOsuReader.Read(reader, ruleset);
		}
		protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard storyboard = null)
			=> new ShredSongWorkingBeatmap(beatmap, Audio);
		private class ShredSongWorkingBeatmap : TestWorkingBeatmap
		{
			private readonly AudioManager audio;
			public ShredSongWorkingBeatmap(IBeatmap beatmap, AudioManager audio) : base(beatmap, audioManager: audio) { this.audio = audio; }
			protected override Track GetBeatmapTrack()
			{
				return audio.GetTrackStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(ShredRuleset).Assembly), "Resources")).Get("shred-demo.wav");
			}
		}
	}
}
