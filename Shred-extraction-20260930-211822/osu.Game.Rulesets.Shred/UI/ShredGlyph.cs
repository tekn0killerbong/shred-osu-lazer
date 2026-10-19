using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;
namespace osu.Game.Rulesets.Shred.UI
{
	// Notes have a solid fill; fret receptors keep their hollow outline.
	public partial class ShredGlyph : Container
	{
		private readonly Box fill;
		private readonly bool solid;
		public ShredGlyph(Color4 colour, bool diamond = false, float shear = -70f / 150f, bool solid = false)
		{
			this.solid = solid;
			if (diamond)
				Rotation = 45;
			else
				Shear = new Vector2(shear, 0);
			fill = new Box { RelativeSizeAxes = Axes.Both, Colour = colour, Alpha = 0.12f };
			Add(fill);
			if (solid)
			{
				fill.Alpha = 1;
				return;
			}
			Add(new Box { RelativeSizeAxes = Axes.X, Height = 2, Colour = colour });
			Add(new Box { RelativeSizeAxes = Axes.X, Height = 2, Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, Colour = colour });
			Add(new Box { RelativeSizeAxes = Axes.Y, Width = 2, Colour = colour });
			Add(new Box { RelativeSizeAxes = Axes.Y, Width = 2, Anchor = Anchor.TopRight, Origin = Anchor.TopRight, Colour = colour });
		}
		public void SetPressed(bool pressed)
		{
			if (solid) return;
			fill.Alpha = 0.12f;
			if (pressed)
				fill.Alpha = 0.8f;
		}
	}
}
