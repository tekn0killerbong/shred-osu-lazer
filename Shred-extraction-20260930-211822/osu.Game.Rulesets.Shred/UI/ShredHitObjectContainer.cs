using osu.Framework.Graphics;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Shred.UI
{
	public partial class ShredHitObjectContainer : HitObjectContainer
	{
		protected override int Compare(Drawable x, Drawable y)
		{
			if (x is DrawableHitObject a && y is DrawableHitObject b)
			{
				// Ascending draw order: upstream/newer heads are painted last.
				int order = a.HitObject.StartTime.CompareTo(b.HitObject.StartTime);
				if (order != 0) return order;
			}
			return base.Compare(x, y);
		}
	}
}
