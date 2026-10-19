using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;

namespace osu.Game.Rulesets.Shred.Edit
{
    public partial class ShredStepRecorderToggle : FillFlowContainer
    {
        public ShredStepRecorderToggle(BindableBool enabled, BindableBool returnOnPause)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Children = new Drawable[]
            {
                new ShredStepRecorderHeader(enabled),
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Left = 42, Right = 8, Bottom = 8 },
                    Spacing = new Vector2(0, 5),
                    Children = new Drawable[]
                    {
                        new OsuCheckbox { RelativeSizeAxes = Axes.X, LabelText = "Return on pause", Current = returnOnPause }
                    }
                }
            };
        }

    }

	public partial class ShredStepRecorderHeader : Container
	{
		public ShredStepRecorderHeader(BindableBool enabled)
		{
			RelativeSizeAxes = Axes.X;
			Height = 44;
			Padding = new MarginPadding { Left = 8, Right = 8 };
			// Native switch at the right; label and icon have their own slots.
			Add(new OsuCheckbox
			{
				RelativeSizeAxes = Axes.X,
				Anchor = Anchor.CentreLeft,
				Origin = Anchor.CentreLeft,
				LabelText = string.Empty,
				Current = enabled
			});
			Add(new SpriteText
			{
				Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft,
				X = 34, Text = "Step recorder",
				Font = FontUsage.Default.With(size: 18)
			});
			var icon = new Container
			{
				Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft,
				Size = new Vector2(26, 26)
			};
			Add(icon);
			for (int side = 0; side < 2; side++)
			{
				float rotation = -15;
				if (side == 1) rotation = 15;
				var foot = new Container
				{
					Position = new Vector2(4 + side * 11, 3 + side * 5),
					Size = new Vector2(6, 16), Rotation = rotation
				};
				foot.Add(new Circle { Size = new Vector2(6, 10) });
				foot.Add(new Circle { Position = new Vector2(1, 12), Size = new Vector2(4) });
				icon.Add(foot);
			}
		}
	}
}
