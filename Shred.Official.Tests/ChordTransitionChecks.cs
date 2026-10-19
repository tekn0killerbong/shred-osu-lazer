using osu.Game.Rulesets.Shred.Gameplay;

internal static partial class GameplayChecks
{
    public static void ProbeTransitions()
    {
        int total=0, expectedBreaks=0;
        foreach(double duration in new[]{4000d,30000})
        foreach (string gesture in new[]{"pick", "hammer-pull", "hammer-pick"})
        foreach (double gap in new[]{-200d,0,200})
        foreach (int fromRow in new[]{0,1})
        foreach (double release in new[]{-500d,-350,-100,-20,0,20,100,500})
        foreach (double press in new[]{-75d,0,50})
        foreach (bool reverse in new[]{false,true})
        {
            using var g=new TestSession();
            int oldKey=100+9*fromRow, newKey=100+9*(1-fromRow);
            bool hammer=gesture=="hammer-pull";
            double endTime=1000+duration;
            double targetTime=endTime+gap;
            var oldA=g.Add(1000,fromRow,3,duration:duration);
            var oldB=g.Add(1000,fromRow,4,duration:duration);
            var type=hammer ? ShredPracticeArticulation.HammerPull : ShredPracticeArticulation.Pick;
            var nextA=g.Add(targetTime,1-fromRow,1,type);
            var nextB=g.Add(targetTime,1-fromRow,2,type);
            if(gesture=="hammer-pick") {
                var seedA=g.Add(700,1-fromRow,1); var seedB=g.Add(700,1-fromRow,2);
                g.Seek(650);g.Press(newKey);g.Press(newKey+1);
                g.Seek(700);g.Press(newKey+5);g.Press(newKey+6);
                if(!seedA.HeadHit || !seedB.HeadHit) throw new Exception("Hammer-pick seed failed");
                g.Seek(750);g.Release(newKey);g.Release(newKey+1);
            }
            g.Seek(900); g.Press(oldKey+2);g.Press(oldKey+3);
            g.Seek(1000);g.Press(oldKey+5);g.Press(oldKey+6);
            if(!oldA.HeadHit || !oldB.HeadHit) throw new Exception("Transition setup failed");
            var events=new List<(double time,Action action)> {
                (endTime+release,()=>g.Release(oldKey+2)),
                (endTime+release+5,()=>g.Release(oldKey+3)),
                (targetTime+press,()=>g.Press(newKey+(reverse ? 1 : 0))),
                (targetTime+press+15,()=>g.Press(newKey+(reverse ? 0 : 1)))
            };
            if(gesture=="pick") { events.Add((targetTime,()=>g.Press(newKey+5)));events.Add((targetTime+5,()=>g.Press(newKey+6))); }
            if(gesture=="hammer-pick") {
                events.Add((targetTime,()=>g.Release(oldKey+5)));events.Add((targetTime+5,()=>g.Release(oldKey+6)));
                events.Add((targetTime+25.01,()=>g.Field.ProcessPendingPicks()));
            }
            foreach(var e in events.OrderBy(e=>e.time)) {g.Seek(e.time);e.action();}
            total++;
            bool expectedHit = !hammer || release >= -350;
            if (nextA.HeadHit != expectedHit || nextB.HeadHit != expectedHit)
                throw new Exception($"Unexpected transition: gesture={gesture} duration={duration} gap={gap} fromRow={fromRow+1} release={release} press={press} reverse={reverse} old={oldA.Result?.Type}/{oldB.Result?.Type} next={nextA.Result?.Type}/{nextB.Result?.Type}");
            if(!expectedHit) expectedBreaks++;
        }
        Console.WriteLine($"PASS {total} transition cases: {total-expectedBreaks} accepted chords; {expectedBreaks} expected hammer-pull chain breaks after LN release 500 ms early. No other blocked chord.");
    }
}
