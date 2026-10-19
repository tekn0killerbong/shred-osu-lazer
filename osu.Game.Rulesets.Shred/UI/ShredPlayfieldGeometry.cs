using System;
using osuTK;

namespace osu.Game.Rulesets.Shred.UI
{
	public static class ShredPlayfieldGeometry
	{
		public const float VisualScale = 1.3f;

        public static float FirstPickCentre(osu.Game.Rulesets.Shred.Skinning.ShredSkinSettings skin)
        {
            float minimum = skin.FretCentre(5) + skin.ColumnWidth[4] / 2 + skin.SectionGap + 72;
            // Mirror the two hand-section centres around the playfield centre,
            // retaining the skin's minimum gap for custom geometries.
            return Math.Max(minimum, -skin.OpenCentre - 1.5f * 65);
        }

		public static Vector2 GuitarHeroReceiver(osu.Game.Rulesets.Shred.Skinning.ShredSkinSettings skin, Vector2 size, float unit, int fret)
		{
			float x = skin.OpenCentre;
			if (fret > 0 && fret <= 5) x = skin.FretCentre(fret);
			if (fret > 5) x = FirstPickCentre(skin) + 1.5f * 65;
			return new Vector2(size.X / 2 + x * unit, size.Y / 2);
		}

		// Distance from a receptor to the first edge reached along the incoming ray.
		public static float DistanceToEdge(Vector2 origin, Vector2 direction, Vector2 size)
		{
			float distance = float.PositiveInfinity;
			if (direction.X > 0) distance = Math.Min(distance, (size.X - origin.X) / direction.X);
			if (direction.X < 0) distance = Math.Min(distance, -origin.X / direction.X);
			if (direction.Y > 0) distance = Math.Min(distance, (size.Y - origin.Y) / direction.Y);
			if (direction.Y < 0) distance = Math.Min(distance, -origin.Y / direction.Y);
			if (!float.IsFinite(distance)) return 0;
			return Math.Max(0, distance);
		}
	}
}
