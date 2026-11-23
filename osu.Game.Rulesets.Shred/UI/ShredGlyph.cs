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
        private readonly bool lightOnly;
        private readonly Color4 idleColour;
		public ShredGlyph(Color4 colour, bool diamond = false, float shear = -70f / 150f, bool solid = false, bool lightOnly = false)
		{
			this.solid = solid;
            this.lightOnly = lightOnly;
            idleColour = colour;
			if (diamond)
				Rotation = 45;
			else
				Shear = new Vector2(shear, 0);
			fill = new Box { RelativeSizeAxes = Axes.Both, Colour = colour, Alpha = lightOnly ? 0 : 0.12f };
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
		public void SetPressed(bool pressed, Color4? pressedColour = null)
		{
			if (solid) return;
			fill.Colour = idleColour;
            fill.Alpha = lightOnly ? 0 : 0.12f;
            if (pressed && !lightOnly)
            {
                fill.Alpha = 0.8f;
                if (pressedColour.HasValue) { fill.Colour = pressedColour.Value; fill.Alpha = 1; }
            }
		}
	}
}
