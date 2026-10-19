using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;
namespace osu.Game.Rulesets.Shred.UI
{
	public partial class ShredGlow : Container
	{
		public ShredGlow(Color4 colour, float radius, float shear, bool hollow = false)
		{
			Shear = new Vector2(shear, 0);
			Blending = BlendingParameters.Additive;
            float fillAlpha = 0.015f;
            if (hollow) fillAlpha = 0;
			Add(new Container
			{
				RelativeSizeAxes = Axes.Both,
				Masking = true,
				CornerRadius = 1,
				BorderThickness = 1,
				BorderColour = colour,
				EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Glow, Colour = colour, Radius = radius, Roundness = 1, Hollow = hollow },
				Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = fillAlpha, Colour = colour },
			});
		}
	}
	public partial class ShredAnimatedSprite : Sprite
	{
		private readonly Texture[] frames;
		private readonly double frameTime;
		private double start;
		public ShredAnimatedSprite(Texture[] frames, float fps)
		{
			this.frames = frames;
			frameTime = 1000 / fps;
			Texture = frames[0];
		}
		protected override void LoadComplete() { base.LoadComplete(); start = Time.Current; }
		public void Restart() { start = Time.Current; Texture = frames[0]; }
		protected override void Update()
		{
			base.Update();
			int index = (int)(Math.Max(0, Time.Current - start) / frameTime) % frames.Length;
			Texture = frames[index];
		}
	}
}
