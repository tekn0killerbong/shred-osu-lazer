using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Input;

internal static partial class GameplayChecks
{
    public static void RunRevision43()
    {
        foreach(int row in new[]{0,1})
        foreach(double offset in new[]{0.093023255813d,0.5,0.999})
        foreach(bool reverse in new[]{false,true})
        {
            using var game=new TestSession();
            var a=game.Add(1000,row,1);var b=game.Add(1000+offset,row,2);
            int key=100+row*9;
            game.Seek(1000);game.Press(key+(reverse ? 1 : 0));game.Press(key+(reverse ? 0 : 1));
            game.Seek(1050);game.Press(key+5);
            if(new[]{a,b}.Count(n=>n.HeadHit)!=1) throw new Exception("One pick must hit exactly one chord member");
            game.Seek(1055);game.Press(key+6);
            if(!a.HeadHit || !b.HeadHit) throw new Exception("Sub-millisecond chord failed");
        }
        var sequence=new ShredPickSequence();
        sequence.Press(1,0,500,500);sequence.Press(1,1,500,501);
        sequence.Press(0,0,1000,1000);sequence.Press(0,1,1000.093,1001);
        if(sequence.Release(0,1)!=0 || sequence.Release(0,0)!=48) throw new Exception("Fractional chord picks must reactivate together");
        sequence.Reset();sequence.Press(0,0,1000,1000);sequence.Press(0,1,1005,1005);
        if(sequence.Release(0,1)!=1) throw new Exception("Distinct 5 ms notes must keep separate pick gestures");
        if(ShredChordRules.SameChordTime(1000,1001)) throw new Exception("Chord tolerance must stay below 1 ms");
        Console.WriteLine("PASS v43 fractional chords preserve pick counts, press order independence and separate 5 ms gestures");
    }
}
