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

internal static partial class Program
{
    private static readonly BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void RunRevision16Tests()
    {
        var cases = new (string name, Action run)[]
        {
            ("All eight physical pick keys survive press and reverse-release order", () =>
            {
                using var b = new LiveBindings();
                Key[] keys = { Key.K, Key.L, Key.Semicolon, Key.Quote, Key.M, Key.Comma, Key.Period, Key.Slash };
                int[] actions = { 105,106,107,108,114,115,116,117 };
                for (int i=0;i<8;i++)
                {
                    b.Press(keys[i]);
                    check(b.Listener.Held.SetEquals(actions.Take(i+1).Select(a => (ShredAction)a)));
                }
                for (int i=7;i>=0;i--)
                {
                    b.Release(keys[i]);
                    check(b.Listener.Held.SetEquals(actions.Take(i).Select(a => (ShredAction)a)));
                }
            }),
            ("Custom overlapping pick combinations do not cancel already held picks", () =>
            {
                using var b = new LiveBindings(new IKeyBinding[] {
                    new KeyBinding(InputKey.L, ShredAction.Row1Pick3),
                    new KeyBinding(new KeyCombination(InputKey.L, InputKey.Semicolon), ShredAction.Row1Pick4),
                    new KeyBinding(InputKey.Period, ShredAction.Row2Pick3),
                    new KeyBinding(InputKey.Slash, ShredAction.Row2Pick4)
                });
                b.Press(Key.L); b.Press(Key.Semicolon); b.Press(Key.Period); b.Press(Key.Slash);
                equal(4,b.Listener.Held.Count);
                b.Release(Key.Semicolon); equal(3,b.Listener.Held.Count);
                check(b.Listener.Held.Contains(ShredAction.Row1Pick3));
            }),
            ("One action with alternative keys remains held until its last key releases", () =>
            {
                using var b = new LiveBindings(new IKeyBinding[] {
                    new KeyBinding(InputKey.K, ShredAction.Row1Pick1), new KeyBinding(InputKey.L, ShredAction.Row1Pick1)
                });
                b.Press(Key.K);b.Press(Key.L);b.Release(Key.K);equal(1,b.Listener.Held.Count);
                b.Release(Key.L);equal(0,b.Listener.Held.Count);
            }),
            ("Fret direction follows new presses and reverses without releasing the row", () =>
            {
                var sequence = new ShredFretSequence();
                sequence.Press(2,0);sequence.Press(4,4);equal(1,sequence.Direction);
                check(ShredChordRules.IsHammerPull(20,4,4,false,0,sequence.Direction));
                sequence.Press(0,4);equal(-1,sequence.Direction);
                check(ShredChordRules.IsHammerPull(5,4,4,false,0,sequence.Direction));
            }),
            ("Extras between chord frets or ahead on the other row block the chord", () =>
            {
                check(!ShredChordRules.IsExact(14,10,0,-1));
                check(!ShredChordRules.IsExact(10,10,0,-1,1));
                check(!ShredChordRules.IsExact(10,10,0,1,16));
                check(ShredChordRules.IsExact(10,10,0,-1,16));
                check(ShredChordRules.IsExact(10,10,0,1,1));
            }),
            ("Held sustains do not block attacks on new frets in either direction", () =>
            {
                check(ShredChordRules.IsExact(21,4,17,-1));
                check(ShredChordRules.IsExact(21,4,17,1));
                check(!ShredChordRules.IsExact(17,4,17,1));
            }),
            ("Open HP needs an actual last-fret release", () =>
            {
                check(ShredChordRules.IsHammerPull(4,0,0,false));
                check(!ShredChordRules.IsHammerPull(0,0,0,false));
                check(!ShredChordRules.IsHammerPull(4,0,0,true));
                check(!ShredChordRules.IsHammerPull(5,4,0,false));
            }),
            ("Three-fret chord needs three pick edges and cannot reuse held picks", () =>
            {
                using var game = new TestSession();
                var notes = new[] {game.Add(1000,0,1),game.Add(1000,0,2),game.Add(1000,0,3)};
                game.Seek(1000);game.Press(100);game.Press(101);game.Press(102);
                game.Press(105);game.Press(105);check(notes[0].HeadHit && notes.Skip(1).All(n=>!n.HeadJudged));
                game.Press(106);check(notes.Take(2).All(n=>n.HeadHit) && !notes[2].HeadJudged);
                game.Press(107);check(notes.All(n=>n.HeadHit));
            }),
            ("Two rows accumulate their own chord picks independently", () =>
            {
                using var game = new TestSession();
                var a=game.Add(1000,0,1);var b=game.Add(1000,0,2);
                var c=game.Add(1000,1,1);var d=game.Add(1000,1,2);
                game.Seek(1000);foreach(int k in new[]{100,101,109,110})game.Press(k);
                game.Press(105);game.Press(114);check(a.HeadHit&&c.HeadHit&&!b.HeadJudged&&!d.HeadJudged);
                game.Press(106);check(a.HeadHit&&b.HeadHit&&c.HeadHit&&!d.HeadJudged);
                game.Press(115);check(c.HeadHit&&d.HeadHit);
            }),
            ("HP chain requires successful pick and breaks on a miss from either row", () =>
            {
                using var game = new TestSession();
                var first=game.Add(1000,0,1,ShredPracticeArticulation.HammerPull);
                var next=game.Add(1500,1,2,ShredPracticeArticulation.HammerPull);
                var missed=game.Add(2000,0,5,ShredPracticeArticulation.HammerPull);
                var after=game.Add(2500,1,3,ShredPracticeArticulation.HammerPull);
                game.Seek(1000);game.Press(100);check(!first.HeadJudged);
                game.Press(105);check(first.Result.Type==HitResult.Good);game.Release(100);game.Release(105);
                game.Seek(1500);game.Press(110);check(next.Result.Type==HitResult.Great);game.Release(110);
                game.Seek(2500);game.Press(111);check(missed.Result.Type==HitResult.Miss&&!after.HeadJudged);
                game.Press(114);check(after.Result.Type==HitResult.Good);
            }),
            ("Open HP after a successful pick receives GREAT on release", () =>
            {
                using var game=new TestSession();var start=game.Add(1000,0,3);var open=game.Add(1500,0,0,ShredPracticeArticulation.HammerPull);
                game.Seek(1000);game.Press(102);game.Press(105);check(start.HeadHit);
                game.Seek(1500);game.Release(102);check(open.Result.Type==HitResult.Great);
            }),
            ("Picked HP sustain head receives GOOD and keeps its hold", () =>
            {
                using var game=new TestSession();var ln=game.Add(1000,0,2,ShredPracticeArticulation.HammerPull,1000);
                game.Seek(1000);game.Press(101);game.Press(105);
                check(ln.IsHolding);
                var head=(DrawableShredHoldHead)typeof(DrawableShredHitObject).GetField("head",hidden).GetValue(ln);
                check(head.Result.Type==HitResult.Good);
            }),
            ("Conflicting LN tail retreats one subdivision only when another LN is active", () =>
            {
                var ln=new ShredHitObject{Fret=1,StartTime=0,Duration=500};
                var shortNote=new ShredHitObject{Fret=1,StartTime=500};
                var held=new ShredHitObject{Fret=3,StartTime=250,Duration=500};
                ShredEditorOperations.ResolveTailConflicts(new[]{ln,shortNote},t=>t-125,_=>{});equal(500,ln.EndTime);
                ShredEditorOperations.ResolveTailConflicts(new[]{ln,shortNote,held},t=>t-125,_=>{});equal(375,ln.EndTime);equal(500,shortNote.StartTime);
            }),
            ("Tail conflict is row-local and also applies when the other LN starts at that time", () =>
            {
                var ln=new ShredHitObject{Fret=1,StartTime=0,Duration=500};
                var shortNote=new ShredHitObject{Fret=1,StartTime=500};
                var held=new ShredHitObject{Row=1,Fret=3,StartTime=500,Duration=500};
                ShredEditorOperations.ResolveTailConflicts(new[]{ln,shortNote,held},t=>t-125,_=>{});equal(500,ln.EndTime);
                held.Row=0;ShredEditorOperations.ResolveTailConflicts(new[]{ln,shortNote,held},t=>t-125,_=>{});equal(375,ln.EndTime);
            }),
            ("Placing an unrelated note does not repair or move old tails elsewhere", () =>
            {
                var ln=new ShredHitObject{Fret=1,StartTime=0,Duration=500};
                var shortNote=new ShredHitObject{Fret=1,StartTime=500};
                var held=new ShredHitObject{Fret=3,StartTime=250,Duration=500};
                var added=new ShredHitObject{Row=1,Fret=2,StartTime=1000};
                ShredEditorOperations.ResolveTailConflicts(new[]{ln,shortNote,held,added},t=>t-125,_=>{},new[]{added});
                equal(500,ln.EndTime);
            }),
            ("A missing simultaneous note on the other row does not block this row", () =>
            {
                using var game=new TestSession();var note=game.Add(1000,0,1);game.Add(1000,1,5);
                game.Seek(1000);game.Press(100);game.Press(105);check(note.HeadHit);
            }),
            ("Releasing an extra fret on the opposite row resolves an armed pick", () =>
            {
                using var game=new TestSession();var note=game.Add(1000,0,3);
                game.Seek(1000);game.Press(102);game.Press(109);game.Press(105);check(!note.HeadJudged);
                game.Release(109);check(note.HeadHit);
            }),
            ("Previous subdivision respects timing changes", () =>
            {
                var timing=new ControlPointInfo();timing.Add(0,new TimingControlPoint{BeatLength=500});timing.Add(220,new TimingControlPoint{BeatLength=400});
                equal(125,ShredGridTicks.Previous(timing,4,220));equal(220,ShredGridTicks.Previous(timing,4,320));
            }),
            ("Selection scrolling continues beyond centre in both directions", () =>
            {
                equal(-1,ShredMarquee.ScrollStrength(300,500,0));equal(-1,ShredMarquee.ScrollStrength(200,500,1));
                equal(1,ShredMarquee.ScrollStrength(0,500,0));equal(1,ShredMarquee.ScrollStrength(500,500,1));
            }),
            ("Marquee includes note edges immediately, without crossing their centres", () =>
            {
                check(ShredMarquee.Intersects(1000,1000,900,1100,100,0,1000,0,0,110,130,24));
                check(!ShredMarquee.Intersects(1000,1000,900,1100,100,0,1000,0,0,125,130,24));
            }),
            ("Cross-row marquee retains musical range behind judgement after scrolling", () =>
            {
                var range=ShredMarquee.Range(1,0,1,3000,3500,1000,2000);
                equal(1000,range.from);equal(3500,range.to);
                check(ShredMarquee.Intersects(1500,1500,range.from,range.to,100,50,1000,1000,2000,70,130,20));
            }),
            ("Marquee shrinks again and can select a sustain whose head is earlier", () =>
            {
                check(!ShredMarquee.Intersects(1800,1800,1000,1500,100,0,1000,0,0,50,150,20));
                check(ShredMarquee.Intersects(500,2000,1000,1500,100,0,1000,0,0,50,150,20));
            }),
            ("GOOD is registered with the native score processor", () =>
            {
                var ruleset=new ShredRuleset();check(ruleset.GetValidHitResults().Contains(HitResult.Good));
                var note=new ShredHitObject{Fret=2,Articulation=ShredPracticeArticulation.HammerPull};
                note.ApplyDefaults(new ControlPointInfo(),new BeatmapDifficulty());
                var map=new Beatmap<ShredHitObject>();map.HitObjects.Add(note);
                var score=new ScoreProcessor(ruleset);score.ApplyBeatmap(map);
                score.ApplyResult(new osu.Game.Rulesets.Judgements.JudgementResult(note,note.Judgement){Type=HitResult.Good});
                equal(1,score.JudgedHits);check(score.Accuracy.Value>0&&score.Accuracy.Value<1);
            }),
            ("Timing distribution excludes only long-note completions", () =>
            {
                var ln=new ShredHitObject{Fret=2,Duration=500};var shortNote=new ShredHitObject{Fret=2};
                ln.ApplyDefaults(new ControlPointInfo(),new BeatmapDifficulty());shortNote.ApplyDefaults(new ControlPointInfo(),new BeatmapDifficulty());
                var objects=new HitObject[]{ln,ln.NestedHitObjects[0],shortNote};
                using var graph=new HitEventTimingDistributionGraph(objects.Select(n=>new HitEvent(0,1,HitResult.Great,n,null,null)).ToArray());
                var retained=(IReadOnlyList<HitEvent>)typeof(HitEventTimingDistributionGraph).GetField("hitEvents",hidden).GetValue(graph);
                equal(2,retained.Count);check(retained.All(e=>e.HitObject!=ln));
            }),
            ("Autoplay uses row-local varied picks and five distinct edges for five frets", () =>
            {
                var map=new Beatmap<ShredHitObject>();
                for(int i=1;i<=5;i++)map.HitObjects.Add(new ShredHitObject{Row=1,Fret=i,StartTime=1000});
                var frames=new ShredAutoGenerator(map).Generate().Frames.OfType<ShredReplayFrame>().ToArray();
                var previous=new HashSet<ShredAction>();var picks=new List<ShredAction>();
                foreach(var frame in frames){picks.AddRange(frame.Actions.Except(previous).Where(a=>(int)a>=114&&(int)a<=117));previous=frame.Actions.ToHashSet();}
                equal(5,picks.Count);equal(4,picks.Distinct().Count());check(!frames.Last().Actions.Any());
            }),
            ("Band Hero uses equal travel distance for every fret", () =>
            {
                foreach(bool hero in new[]{true})
                {
                    using var game=new TestSession(hero);
                    game.Field.RelativeSizeAxes=Axes.None;game.Field.Size=new Vector2(1000,720);game.Seek(1000);
                    double distance=-1;
                    for(int i=0;i<=5;i++)
                    {
                        var note=new ShredHitObject{Row=1,Fret=i};
                        double current=(game.Field.PositionAt(note,1500)-game.Field.Receiver(1,i)).Length;
                        if(distance>=0)check(Math.Abs(distance-current)<0.001);distance=current;
                    }
                }
                check(new ShredModGuitarHero().Name=="Band Hero");
            }),
            ("Bottom pick row is left of the top pick row", () =>
            {
                using var field=new ShredPlayfield{RelativeSizeAxes=Axes.None,Size=new Vector2(1000,720)};
                for(int i=7;i<=10;i++)check(field.Receiver(1,i).X<field.Receiver(0,i).X);
            }),
            ("Highlighted target row controls paste regardless of source row", () =>
            {
                equal(1, ShredEditorOperations.PasteRow(0,0,1));
                equal(0, ShredEditorOperations.PasteRow(1,1,0));
                equal(0, ShredEditorOperations.PasteRow(1,0,1));
                equal(1, ShredEditorOperations.PasteRow(1,0,0));
            }),
            ("Autoplay frames actually play chords, sustains and HP across both rows", () =>
            {
                using var game=new TestSession();
                var notes=new[]{game.Add(1000,0,1),game.Add(1000,0,3),
                    game.Add(1500,1,2,ShredPracticeArticulation.HammerPull),
                    game.Add(2000,0,2,ShredPracticeArticulation.Pick,1000),
                    game.Add(2250,0,4,ShredPracticeArticulation.Pick,1000),
                    game.Add(3500,1,0,ShredPracticeArticulation.HammerPull)};
                var map=new Beatmap<ShredHitObject>();map.HitObjects.AddRange(notes.Select(n=>n.HitObject));
                var previous=new HashSet<ShredAction>();
                foreach(var frame in new ShredAutoGenerator(map).Generate().Frames.OfType<ShredReplayFrame>())
                {
                    game.Seek(frame.Time);
                    foreach(var action in previous.Except(frame.Actions).ToArray())game.Release((int)action);
                    foreach(var action in frame.Actions.Except(previous).ToArray())game.Press((int)action);
                    previous=frame.Actions.ToHashSet();
                }
                check(notes.All(n=>n.HeadHit));check(notes.Where(n=>n.HitObject.IsLong).All(n=>n.Result.IsHit));
            }),
            ("Missed LN darkens progressively over 160 milliseconds", () =>
            {
                var manual=new ManualClock();var clock=new FramedClock(manual);
                using var visual=new ShredNoteVisual(new ShredHitObject{Fret=2,Duration=500},new ShredSkinSettings()){Clock=clock};
                var initial=visual.Colour;
                visual.Layout(new Vector2(0,100),1,failed:true);
                check(visual.Colour.Equals(initial));
                manual.CurrentTime=80;clock.ProcessFrame();visual.Layout(new Vector2(0,100),1,failed:true);
                var midway=visual.Colour;check(!midway.Equals(initial));
                manual.CurrentTime=160;clock.ProcessFrame();visual.Layout(new Vector2(0,100),1,failed:true);
                check(!visual.Colour.Equals(midway));
                visual.Layout(new Vector2(0,100),1,failed:false);
                check(visual.Colour.Equals(initial));
            }),
            ("Held LN keeps a complete bright head", () =>
            {
                using var visual=new ShredNoteVisual(new ShredHitObject{Fret=2,Duration=500},new ShredSkinSettings()) {RelativeSizeAxes=Axes.None,Size=new Vector2(40,26)};
                visual.Layout(new Vector2(0,100),1,holding:true);
                var head=(ShredGlyph)typeof(ShredNoteVisual).GetField("head",hidden).GetValue(visual);
                equal(1,head.Alpha);check(head.Colour.Equals((osu.Framework.Graphics.Colour.ColourInfo)ShredPlayfield.FretColour(2)));
            })
        };
        foreach(var item in cases){item.run();Console.WriteLine("PASS "+item.name);}
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
            Seek(manual.CurrentTime + 20.01 * rate);
            Field.ProcessPendingPicks();
        }
        public void Press(int action)=>Field.OnPressed(new KeyBindingPressEvent<ShredAction>(input,(ShredAction)action));
        public void Release(int action)=>Field.OnReleased(new KeyBindingReleaseEvent<ShredAction>(input,(ShredAction)action));
        public TestNote Add(double time,int row,int fret,ShredPracticeArticulation type=ShredPracticeArticulation.Pick,double duration=0)
        {
            var note=new ShredHitObject{StartTime=time,Row=row,Fret=fret,Articulation=type,Duration=duration};
            note.ApplyDefaults(new ControlPointInfo(),new BeatmapDifficulty());
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
