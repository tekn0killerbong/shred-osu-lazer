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

		// Hit testing follows the rendered bar; the editor's wider snap band is
		// deliberately independent of this shape.
		public static bool ContainsVisibleBar(Vector2 point, Vector2 start, Vector2 end, float height, float shear, float unit)
		{
			float halfHeight = height / 2;
			float y = point.Y - start.Y;
			float straightX = point.X;
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
			centre.X += (skin.ColumnWidth[4] / 2 + Math.Max(0, skin.RowGap - skin.NoteHeight)) * unit + lineWidth / 2;
			return centre;
		}

		// PositionAt(StartTime/EndTime) is an edge, never the centre of a head.
		// Approach is from the right: first contact is with the line's right face.
		public static Vector2 ContactEdge(Vector2 judgementCentre, float lineWidth)
			=> judgementCentre + new Vector2(lineWidth / 2, 0);
	}
}
