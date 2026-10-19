using System;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;

internal static partial class Program
{
    private static void RunRevision17Tests()
    {
        var cases = new (string name, Action run)[]
        {
            ("Every newly pressed fret outranks every combination of older held frets", () =>
            {
                for (int held = 1; held < 32; held++)
                for (int target = 0; target < 5; target++)
                {
                    int bit = 1 << target;
                    if ((held & bit) != 0) continue;
                    var sequence = new ShredFretSequence();
                    int previous = 0;
                    for (int i = 0; i < 5; i++)
                        if ((held & (1 << i)) != 0)
                        {
                            sequence.Press(i, previous);
                            previous |= 1 << i;
                        }
                    sequence.Press(target, held);
                    check(sequence.IsExact(held | bit, bit, 0));
                    check(sequence.IsHammerPull(held, held | bit, bit, true, 0));
                    for (int i = 0; i < 5; i++)
                        if ((held & (1 << i)) != 0)
                            check(!sequence.IsExact(held | bit, 1 << i, 0));
                }
            }),
            ("Released fret cannot reverse direction relative to the held anchor", () =>
            {
                var sequence = new ShredFretSequence();
                sequence.Press(2, 0); sequence.Press(4, 4);
                sequence.Press(3, 4); equal(1, sequence.Direction);
                check(sequence.IsExact(12, 8, 0));
                sequence.Reset();
                sequence.Press(2, 0); sequence.Press(0, 4);
                sequence.Press(1, 4); equal(-1, sequence.Direction);
                check(sequence.IsExact(6, 2, 0));
            }),
            ("Pull off returns priority to the newest remaining hold", () =>
            {
                var sequence = new ShredFretSequence();
                sequence.Press(0, 0); sequence.Press(4, 1); sequence.Press(2, 17);
                check(sequence.IsExact(21, 4, 0));
                check(sequence.IsHammerPull(21, 17, 16, false, 0));
                check(!sequence.IsExact(17, 1, 0));
                check(sequence.IsHammerPull(17, 1, 1, false, 0));
            }),
            ("Chord permits older outer holds but still rejects middle extras", () =>
            {
                var sequence = new ShredFretSequence();
                sequence.Press(0, 0); sequence.Press(4, 1);
                sequence.Press(1, 17); sequence.Press(3, 19);
                check(sequence.IsExact(27, 10, 0));
                sequence.Press(2, 27);
                check(!sequence.IsExact(31, 10, 0));
                check(sequence.IsExact(31, 10, 4));
                check(!sequence.IsExact(3, 10, 0));
            }),
            ("Open and chain reset retain their restrictions", () =>
            {
                var sequence = new ShredFretSequence();
                sequence.Press(1, 0);
                check(sequence.IsHammerPull(2, 0, 0, false, 0));
                check(!sequence.IsHammerPull(0, 0, 0, false, 0));
                check(!sequence.IsExact(2, 0, 0));
                sequence.Reset();
                sequence.Press(4, 0); sequence.Press(0, 16);
                check(sequence.IsExact(17, 1, 0));
            }),
            ("Real gameplay accepts reversal after release and between held frets", () =>
            {
                foreach (bool releaseOld in new[] { false, true })
                foreach (bool mirror in new[] { false, true })
                foreach (int row in new[] { 0, 1 })
                {
                    using var game = new TestSession();
                    int key = 100 + row * 9;
                    int outer = mirror ? 1 : 5, target = mirror ? 2 : 4;
                    var root = game.Add(1000, row, 3);
                    var first = game.Add(1500, row, outer, ShredPracticeArticulation.HammerPull);
                    var next = game.Add(2000, row, target, ShredPracticeArticulation.HammerPull);
                    game.Seek(1000); game.Press(key + 2); game.Press(key + 5); game.Release(key + 5);
                    check(root.HeadHit);
                    game.Seek(1500); game.Press(key + outer - 1); check(first.HeadHit);
                    if (releaseOld) game.Release(key + outer - 1);
                    game.Seek(2000); game.Press(key + target - 1); check(next.HeadHit);
                }
            })
        };
        foreach (var item in cases) { item.run(); Console.WriteLine("PASS " + item.name); }
    }
}
