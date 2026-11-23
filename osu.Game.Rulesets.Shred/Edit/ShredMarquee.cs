using System;

namespace osu.Game.Rulesets.Shred.Edit
{
    /// <summary>Musical bounds survive scrolling even when notes leave the visible grid.</summary>
    public static class ShredMarquee
    {
        public static (double from, double to) Range(int row, int originRow, int endRow,
            double start, double end, double initialView, double currentView)
        {
            double from = row == originRow ? start : Math.Min(initialView, currentView);
            double to = row == endRow ? end : currentView;
            return (Math.Min(from, to), Math.Max(from, to));
        }

        public static bool IntersectsColumn(double start, double end, double from, double to,
            float originX, float left, float right, float halfWidth)
        {
            if (start > to + 0.5 || end < from - 0.5) return false;
            return originX + halfWidth >= left && originX - halfWidth <= right;
        }

        public static double ScrollStrength(float y, float height, int originRow)
        {
            const float edge = 60;
            float toEdge = originRow == 0 ? y : height - y;
            float toCentre = originRow == 0 ? height / 2 - y : y - height / 2;
            return Math.Clamp(Math.Max(0, 1 - toEdge / edge) - Math.Max(0, 1 - toCentre / edge), -1, 1);
        }
    }
}
