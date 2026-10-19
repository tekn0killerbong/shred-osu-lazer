using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Mods;
using osu.Game.Rulesets.Shred.Skinning;
using osu.Game.Rulesets.Shred.UI;
using osuTK;

internal static partial class GameplayChecks
{
    public static void RunRevision40()
    {
        int cases = 0;
        foreach (int row in new[] { 0, 1 })
        for (int left = 1; left <= 3; left++)
        for (int right = left + 2; right <= 5; right++)
        for (int middle = left + 1; middle < right; middle++)
        foreach (bool middleLast in new[] { false, true })
        {
            using var game = new TestSession();
            int key = 100 + row * 9;
            var a = game.Add(1000, row, left);
            var b = game.Add(1000, row, right);
            game.Seek(500);
            if (!middleLast) game.Press(key + middle - 1);
            game.Press(key + left - 1); game.Press(key + right - 1);
            if (middleLast) game.Press(key + middle - 1);
            game.Press(100 + (1 - row) * 9 + 4);
            game.Seek(1000); game.Press(key + 5); game.Press(key + 6);
            if (!a.HeadHit || !b.HeadHit) throw new Exception($"Interior fret blocked chord: row {row}, {left}/{middle}/{right}");
            cases++;
        }
        Console.WriteLine($"PASS v40: {cases} picked chords with interior holds, both press orders and rows");
        foreach (int row in new[] { 0, 1 })
        {
            using var game = new TestSession();
            int key = 100 + row * 9, other = 100 + (1 - row) * 9;
            var start = game.Add(1000, row, 2);
            var pull = game.Add(1500, row, 1, ShredPracticeArticulation.HammerPull);
            game.Seek(500); game.Press(key); game.Press(key + 1); game.Press(other + 4);
            game.Seek(1000); game.Press(key + 5);
            if (!start.HeadHit) throw new Exception("HP setup failed");
            game.Seek(1500); game.Release(other + 4);
            if (pull.HeadHit) throw new Exception("Cross-row fret release triggered HP");
            game.Release(key + 1);
            if (!pull.HeadHit) throw new Exception("Same-row pull-off stopped working");
        }
        Console.WriteLine("PASS v40: fret pull-offs stay in their row; same-row pull-offs still hit");
        var skin = new ShredSkinSettings();
        foreach (float unit in new[] { 0.5f, 1f, 2f })
        foreach (float offset in new[] { -70f, 0f, 32.5f, 90f })
        {
            var upper = new Vector2(100, 100);
            var lower = upper + new Vector2(offset, skin.RowGap) * unit;
            float width = Math.Max(1, unit);
            var centre = ShredWhamGeometry.JudgementCentre(skin, upper, lower, unit, width);
            Vector2 normal = Vector2.Normalize(new Vector2(1, skin.Shear(0)));
            float right = Math.Max(Vector2.Dot(upper, normal), Vector2.Dot(lower, normal)) + skin.ColumnWidth[4] * unit * normal.X / 2;
            float left = Vector2.Dot(centre, normal) - width * normal.X / 2;
            if (Math.Abs(left - right - (skin.RowGap - skin.NoteHeight) * unit) > 0.001) throw new Exception("Wham slanted edge spacing mismatch");
        }
        var mod = new ShredModGuitarHero();
        if (mod.Name != "The Original Gamer" || mod.Acronym != "OG") throw new Exception("OG identity");
        Console.WriteLine("PASS v40: wham edge spacing across offsets/scales and OG identity");
    }
}
