using System;
using System.Linq;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Input;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
namespace osu.Game.Rulesets.Shred.UI
{
	[Cached]
	public partial class DrawableShredRuleset : DrawableRuleset<ShredHitObject>
	{
		public bool GuitarHero { get; }
		public DrawableShredRuleset(ShredRuleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod> mods = null) : base(ruleset, beatmap, mods)
		{
			GuitarHero = mods?.Any(m => m is osu.Game.Rulesets.Shred.Mods.ShredModGuitarHero) == true;
		}
		protected override osu.Game.Input.Handlers.ReplayInputHandler CreateReplayInputHandler(osu.Game.Replays.Replay replay)
			=> new osu.Game.Rulesets.Shred.Replays.ShredFramedReplayInputHandler(replay);
		protected override Playfield CreatePlayfield() => new ShredPlayfield();
		public override DrawableHitObject<ShredHitObject> CreateDrawableRepresentation(ShredHitObject h) => new DrawableShredHitObject(h);
		protected override PassThroughInputManager CreateInputManager() => new ShredInputManager(Ruleset.RulesetInfo);
	}
}
