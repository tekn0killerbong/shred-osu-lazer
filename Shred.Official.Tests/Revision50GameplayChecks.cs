using osu.Game.Rulesets.Shred.Gameplay;

internal static partial class GameplayChecks
{
    public static void RunRevision50()
    {
        int cases = 0;
        foreach (int row in new[] { 0, 1 })
        foreach (int count in new[] { 3, 4, 5 })
        foreach (bool reverse in new[] { false, true })
        foreach (bool fretFirst in new[] { false, true })
        foreach (double offset in new[] { -75d, 0, 75d })
        {
            using var game = new TestSession();
            int key = 100 + row * 9;
            var chord = Enumerable.Range(1, count).Select(f => game.Add(1000, row, f)).ToArray();
            game.Seek(1000 + offset);
            void Frets()
            {
                foreach (int f in reverse ? Enumerable.Range(0, count).Reverse() : Enumerable.Range(0, count)) game.Press(key + f);
            }
            if (fretFirst) Frets();
            // A five-note chord can use four picks, then re-press one within its window.
            for (int i = 0; i < count; i++)
            {
                if (i == 4) game.Release(key + 5);
                game.Press(key + 5 + i % 4);
            }
            if (!fretFirst) Frets();
            if (chord.Any(n => !n.HeadHit)) throw new Exception($"v50 {count}-note picked chord blocked: row={row}, reverse={reverse}, fretFirst={fretFirst}, offset={offset}");
            cases++;
        }
        Console.WriteLine($"PASS v50 {cases} chords of 3, 4 and 5 notes, both rows, input orders and offsets");
        // The expanded return capacity added in v50 is withdrawn in v52.
        // Its replacement checks live in Revision52GameplayChecks.
    }
}
