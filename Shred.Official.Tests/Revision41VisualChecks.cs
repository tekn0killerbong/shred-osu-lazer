using System.Reflection;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Skinning;
using osu.Game.Rulesets.Shred.UI;
using osuTK;
using osuTK.Graphics;

internal static class Revision41VisualChecks
{
    public static void Run()
    {
        var skin = new ShredSkinSettings();
        for (int row = 0; row < 2; row++)
        for (int fret = 1; fret <= 5; fret++)
        foreach (bool selected in new[] { false, true })
        {
            using var visual = new ShredNoteVisual(new ShredHitObject { StartTime = 1000, Row = row, Fret = fret, Duration = 1000 }, skin);
            var head = (ShredGlyph)typeof(ShredNoteVisual).GetField("head", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(visual)!;
            visual.Layout(new Vector2(0, -100), 1, holding: false, selected: selected, editor: true);
            var expected = head.Colour;
            visual.Layout(new Vector2(0, -80), 1, holding: true, selected: selected, editor: true);
            Revision39Checks.Check(head.Colour.Equals(expected), "v41 LN head keeps its regular colour at the receptor");
        }
        using var glow = new ShredGlow(Color4.White, 12, 0, hollow: true);
        var border = glow.Children.OfType<Container>().Single();
        Revision39Checks.Check(border.EdgeEffect.Hollow && border.BorderThickness > 0 && border.Children.OfType<Box>().Single().Alpha == 0,
            "v41 foreground glow has an empty interior and a visible perimeter");
        using var glyph = new ShredGlyph(ShredPlayfield.FretColour(3));
        glyph.SetPressed(true);
        var fill = glyph.Children.OfType<Box>().First();
        Revision39Checks.Check(fill.Alpha > 0 && fill.Alpha < 1, "v41 pressed receptor retains translucent coloured fill");
    }
}
