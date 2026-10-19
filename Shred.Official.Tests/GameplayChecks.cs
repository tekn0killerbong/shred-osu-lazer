using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Mods;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Objects.Drawables;
using osu.Game.Rulesets.Shred.Replays;
using osu.Game.Rulesets.Shred.Skinning;
using osu.Game.Rulesets.Shred.UI;
using osu.Game.Screens.Ranking.Statistics;
using osuTK;
using osuTK.Input;

internal static partial class GameplayChecks
{
    private static readonly System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    public static void Run()
    {
        foreach (int row in new[]{0,1})
        foreach (int opposite in new[]{1,2,3,4,5})
        foreach (double offset in new[]{-75d,0,50,100})
        {
            using var game=new TestSession();
            var a=game.Add(1000,row,2); var b=game.Add(1000,row,4);
            int key=100+row*9;
            game.Seek(500);game.Press(key+1);game.Press(key+3);game.Press(100+(1-row)*9+opposite-1);
            game.Seek(1000+offset);game.Press(key+5);game.Press(key+6);
            if(!a.HeadHit || !b.HeadHit) throw new Exception("Cross-row regression");
        }
        Console.WriteLine("PASS Official gameplay: 40 cross-row chord cases");
        foreach(int row in new[]{0,1})
        {
            using var game=new TestSession();int key=100+row*9;
            game.Add(1000,row,1);game.Add(1500,row,1);var miss=game.Add(1800,row,1);var target=game.Add(2200,row,1);
            game.Seek(1000);game.Press(key);game.Press(key+5);game.Seek(1500);game.Press(key+6);
            game.Seek(2100);miss.CheckExpiry();game.Seek(2200);game.Release(key+6);game.FlushPicks();
            if(!target.HeadHit) throw new Exception("Hammer-pick recovery regression");
        }
        Console.WriteLine("PASS Official gameplay: hammer-pick recovery after miss on both rows");
    }
    private sealed class TestSession : IDisposable
    {
        private readonly ManualClock manual=new ManualClock();
        private readonly FramedClock clock;
        private readonly InputState input=new InputState();
        public readonly DrawableShredRuleset Ruleset;
        public readonly ShredPlayfield Field;
        public TestSession(bool hero=false, double rate=1)
        {
            manual.Rate = rate;
            clock=new FramedClock(manual);
            Ruleset=new DrawableShredRuleset(new ShredRuleset(),new Beatmap<ShredHitObject>(), hero ? new osu.Game.Rulesets.Mods.Mod[]{new ShredModGuitarHero()} : null);
            Field=new ShredPlayfield{Clock=clock};
            typeof(ShredPlayfield).GetProperty("drawableRuleset",hidden).SetValue(Field,Ruleset);
        }
        public void Seek(double time){manual.CurrentTime=time;clock.ProcessFrame();}
        public void FlushPicks()
        {
            double rate = double.IsFinite(manual.Rate) && manual.Rate > 0 ? manual.Rate : 1;
            Seek(manual.CurrentTime + 30.01 * rate);
            Field.ProcessPendingPicks();
        }
        public void Press(int action)=>Field.OnPressed(new KeyBindingPressEvent<ShredAction>(input,(ShredAction)action));
        public void Release(int action)=>Field.OnReleased(new KeyBindingReleaseEvent<ShredAction>(input,(ShredAction)action));
        public TestNote Add(double time,int row,int fret,ShredPracticeArticulation type=ShredPracticeArticulation.Pick,double duration=0, float od=5)
        {
            var note=new ShredHitObject{StartTime=time,Row=row,Fret=fret,Articulation=type,Duration=duration};
            note.ApplyDefaults(new ControlPointInfo(),new BeatmapDifficulty { OverallDifficulty = od });
            var drawable=new TestNote(note){Clock=clock};
            typeof(DrawableShredHitObject).GetProperty("field",hidden).SetValue(drawable,Field);
            if(duration>0)
            {
                var head=new TestHead((ShredHoldHead)note.NestedHitObjects[0]){Clock=clock};
                typeof(DrawableShredHoldHead).GetProperty("field",hidden).SetValue(head,Field);
                typeof(DrawableShredHitObject).GetField("head",hidden).SetValue(drawable,head);
            }
            Field.Add(drawable);
            return drawable;
        }
        public void Dispose(){Field.Dispose();Ruleset.Dispose();}
    }
    private partial class TestNote : DrawableShredHitObject
    {
        public TestNote(ShredHitObject note):base(note){}
        public override void PlaySamples(){}
        protected override void UpdateHitStateTransforms(ArmedState state){}
    }
    private partial class TestHead : DrawableShredHoldHead
    {
        public TestHead(ShredHoldHead note):base(note){}
        public override void PlaySamples(){}
        protected override void UpdateHitStateTransforms(ArmedState state){}
    }
}
