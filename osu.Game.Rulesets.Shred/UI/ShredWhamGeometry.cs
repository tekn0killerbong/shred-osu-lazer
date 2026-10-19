using System;
using osu.Game.Rulesets.Shred.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Shred.UI
{
	public static class ShredWhamGeometry
	{
		public static readonly Color4 Colour = new Color4(75, 0, 130, 255);
		public const float Opacity = 1f;
		public const float HookInset = 0;
		public static float LineHeight(ShredSkinSettings skin) => Math.Max(4, skin.RowGap - 10);
		public static Vector2 BarSize(ShredSkinSettings skin, float unit)
			=> new Vector2(8, Math.Min(skin.NoteHeight * 0.6f, LineHeight(skin))) * unit;

		// Hit testing follows the rendered bar; the editor's wider snap band is
		// deliberately independent of this shape.
		public static bool ContainsVisibleBar(Vector2 point, Vector2 start, Vector2 end, float height, float shear, float unit)
		{
			float halfHeight = height / 2;
			float y = point.Y - start.Y;
			float straightX = point.X + shear * y;
			float left = Math.Min(start.X, end.X);
			float right = Math.Max(start.X, end.X);
			if (Math.Abs(y) <= halfHeight + unit && straightX >= left - unit && straightX <= right + unit)
				return true;

			// The short, angled grip is decorative, but right-clicking it should
			// remove the same wham as right-clicking the straight portion.
			Vector2 gripStart = end - new Vector2(HookInset * unit, 0);
			Vector2 direction = new Vector2((float)Math.Cos(Math.PI / 9), (float)Math.Sin(Math.PI / 9));
			Vector2 offset = point - gripStart;
			float along = Vector2.Dot(offset, direction);
			float across = Math.Abs(offset.X * direction.Y - offset.Y * direction.X);
			return offset.Length <= halfHeight + unit
				|| (along >= 0 && along <= 24 * unit && across <= halfHeight + unit);
		}

		public static Vector2 JudgementCentre(ShredSkinSettings skin, Vector2 upperRightFret, Vector2 lowerRightFret, float unit, float lineWidth)
		{
			Vector2 centre = (upperRightFret + lowerRightFret) / 2;
			float shear = skin.Shear(0);
			// Measure beside the slanted sides, not between their axis-aligned
			// bounding boxes (which added a large empty wedge beside the frets).
			float upperEdge = upperRightFret.X + shear * (upperRightFret.Y - centre.Y)
				+ (skin.ColumnWidth[4] + Math.Abs(shear - skin.Shear(0)) * skin.NoteHeight) * unit / 2;
			float lowerEdge = lowerRightFret.X + shear * (lowerRightFret.Y - centre.Y)
				+ (skin.ColumnWidth[4] + Math.Abs(shear - skin.Shear(1)) * skin.NoteHeight) * unit / 2;
			float gap = Math.Max(0, skin.RowGap - skin.NoteHeight) * unit;
			centre.X = Math.Max(upperEdge, lowerEdge) + gap * MathF.Sqrt(1 + shear * shear) + lineWidth / 2;
			return centre;
		}

		// PositionAt(StartTime/EndTime) is an edge, never the centre of a head.
		// Approach is from the right: first contact is with the line's right face.
		public static Vector2 ContactEdge(Vector2 judgementCentre, float lineWidth)
			=> judgementCentre + new Vector2(lineWidth / 2, 0);
	}
}
