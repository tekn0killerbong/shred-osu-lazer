using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Shred.UI
{
	/// <summary>A warm contact glow and a fixed pool of short friction sparks.</summary>
	public partial class ShredWhamGrind : Container
	{
		private const int sparkCount = 12;
		private readonly Container halo;
		private readonly Box core;
		private readonly Box hotSpot;
		private readonly Spark[] sparks = new Spark[sparkCount];
		private readonly Random random = new Random(54);
		private double lastTime = double.NaN;
		private double nextSpark;
		private bool wasGrinding;
		private float strength;
		private int nextSlot;
		private float unit;
		private float shear;

		public override bool HandlePositionalInput => false;
		public override bool HandleNonPositionalInput => false;

		public ShredWhamGrind()
		{
			Origin = Anchor.Centre;
			Blending = BlendingParameters.Additive;
			Add(halo = new Container
			{
				Anchor = Anchor.Centre, Origin = Anchor.Centre, Masking = true, Alpha = 0,
				Child = new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(255, 190, 65, 255), Alpha = 0.25f }
			});
			Add(core = new Box
			{
				Anchor = Anchor.Centre, Origin = Anchor.Centre, Alpha = 0,
				Colour = new Color4(255, 215, 125, 255), EdgeSmoothness = new Vector2(0.7f)
			});
			Add(hotSpot = new Box
			{
				Anchor = Anchor.Centre, Origin = Anchor.Centre, Alpha = 0,
				Colour = new Color4(255, 232, 170, 255), EdgeSmoothness = new Vector2(1)
			});
			for (int i = 0; i < sparkCount; i++)
			{
				Box drawable = new Box
				{
					Anchor = Anchor.Centre, Origin = Anchor.CentreLeft, Alpha = 0,
					Colour = new Color4(255, 200, 85, 255), EdgeSmoothness = new Vector2(0.7f)
				};
				Add(drawable);
				sparks[i] = new Spark(drawable);
			}
		}

		public void Layout(Vector2 centre, Vector2 lineSize, float unit, float shear)
		{
			Position = centre;
			Size = lineSize;
			this.unit = unit;
			this.shear = shear;
			Vector2 angle = new Vector2(shear, 0);
			halo.Shear = core.Shear = hotSpot.Shear = angle;
			halo.Size = new Vector2(Math.Max(lineSize.X, 2 * unit), lineSize.Y);
			halo.CornerRadius = Math.Min(unit, lineSize.Y / 2);
			halo.EdgeEffect = new EdgeEffectParameters
			{
				Type = EdgeEffectType.Glow, Colour = new Color4(255, 178, 50, 255),
				Radius = 10 * unit, Roundness = unit
			};
			core.Size = new Vector2(Math.Max(lineSize.X, 1.5f * unit), lineSize.Y);
			hotSpot.Size = new Vector2(3 * unit, Math.Min(lineSize.Y, 7 * unit));
		}

		public void Animate(double time, bool pressed, bool grinding, bool visible)
		{
			double elapsed = time - lastTime;
			if (!double.IsFinite(elapsed) || elapsed < 0 || elapsed > 250 || !visible)
			{
				reset();
				elapsed = 0;
			}
			lastTime = time;
			if (!visible) return;
			grinding &= pressed;
			float target = 0;
			if (pressed) target = 0.38f;
			if (grinding) target = 0.85f;
			double response = 65;
			if (target > strength) response = 22;
			strength += (target - strength) * (float)(1 - Math.Exp(-elapsed / response));
			if (strength < 0.001f) strength = 0;
			float shimmer = 1;
			if (grinding) shimmer = 0.94f + 0.06f * (float)Math.Sin(time / 37);
			halo.Alpha = strength * shimmer;
			core.Alpha = strength * 0.8f;
			// The bright contact point slides along the slanted judgement edge.
			float y = (float)Math.Sin(time / 73) * DrawHeight * 0.32f;
			hotSpot.Position = new Vector2(-shear * y, y);
			hotSpot.Alpha = 0;
			if (grinding) hotSpot.Alpha = strength * 0.7f;
			if (grinding)
			{
				if (!wasGrinding) nextSpark = time;
				if (time >= nextSpark)
				{
					emit(time, y);
					emit(time, -y);
					nextSpark = time + 55;
				}
			}
			wasGrinding = grinding;
			foreach (Spark spark in sparks)
			{
				if (!spark.Active) continue;
				float age = (float)(time - spark.Start);
				if (age >= spark.Lifetime)
				{
					spark.Active = false;
					spark.Drawable.Alpha = 0;
					continue;
				}
				float progress = age / spark.Lifetime;
				Vector2 offset = spark.Origin + spark.Velocity * age + new Vector2(0, 0.00014f * age * age);
				spark.Drawable.Position = offset * unit;
				spark.Drawable.Size = new Vector2(spark.Length * (1 - progress * 0.65f), 0.9f) * unit;
				spark.Drawable.Alpha = 0.8f * (1 - progress) * (1 - progress);
			}
		}

		private void emit(double time, float y)
		{
			Spark spark = sparks[nextSlot];
			nextSlot = (nextSlot + 1) % sparkCount;
			spark.Start = time;
			spark.Lifetime = 180 + random.Next(100);
			spark.Length = 3 + random.Next(5);
			spark.Origin = new Vector2(-shear * y, y) / Math.Max(0.001f, unit);
			// Fan outward into the incoming wham lane, away from the fret boxes.
			spark.Velocity = new Vector2(0.08f + random.NextSingle() * 0.11f, (random.NextSingle() - 0.6f) * 0.22f);
			spark.Drawable.Rotation = MathF.Atan2(spark.Velocity.Y, spark.Velocity.X) * 180 / MathF.PI;
			spark.Active = true;
		}

		private void reset()
		{
			strength = 0;
			wasGrinding = false;
			halo.Alpha = core.Alpha = hotSpot.Alpha = 0;
			foreach (Spark spark in sparks)
			{
				spark.Active = false;
				spark.Drawable.Alpha = 0;
			}
		}

		private sealed class Spark
		{
			public readonly Box Drawable;
			public Vector2 Origin;
			public Vector2 Velocity;
			public double Start;
			public float Lifetime;
			public float Length;
			public bool Active;
			public Spark(Box drawable) => Drawable = drawable;
		}
	}
}
