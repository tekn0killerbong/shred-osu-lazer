using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Objects.Drawables;

namespace osu.Game.Rulesets.Shred.Mods
{
	public class ShredModHalfTime : ModHalfTime { }
	public class ShredModDoubleTime : ModDoubleTime { }
	public class ShredModSuddenDeath : ModSuddenDeath { }
	public class ShredModHidden : ModHidden
	{
		public override LocalisableString Description => "Notes fade out before reaching the frets.";
		protected override void ApplyIncreasedVisibilityState(DrawableHitObject hitObject, ArmedState state)
		{
			if (hitObject is DrawableShredHitObject note) note.Hidden = false;
		}
		protected override void ApplyNormalVisibilityState(DrawableHitObject hitObject, ArmedState state)
		{
			if (hitObject is DrawableShredHitObject note) note.Hidden = true;
		}
	}
	public class ShredModMirror : ModMirror, IApplicableToHitObject
	{
		public override LocalisableString Description => "Flip objects on the chosen axes.";
		[SettingSource("Flipped axes")]
		public Bindable<MirrorType> Reflection { get; } = new Bindable<MirrorType>();
		public void ApplyToHitObject(HitObject hitObject)
		{
			if (hitObject is not ShredHitObject note || note.IsWham) return;
			if (Reflection.Value != MirrorType.Vertical && note.Fret > 0) note.Fret = 6 - note.Fret;
			if (Reflection.Value != MirrorType.Horizontal) note.Row = 1 - note.Row;
		}
		public enum MirrorType { Horizontal, Vertical, Both }
	}
}
