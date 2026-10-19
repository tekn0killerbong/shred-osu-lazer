using System.Globalization;
using System.Reflection;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Game.Rulesets.Shred.Skinning;
using osu.Game.Screens.Edit;
using osu.Game.Rulesets.Shred.Edit;
using osuTK;
using osu.Game.Rulesets.Shred.UI;
using osu.Framework.Timing;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Shred.Objects.Drawables;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Input;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred.Scoring;

int count = 0;
void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); count++; }
void Reject(Action action, string label) { try { action(); } catch (FormatException) { Check(true,label); return; } throw new Exception(label); }
var state = new ShredStringState();
state.SetFret(0,true); state.SetFret(4,true);
Check(state.FretMask == 17, "Lower and upper frets remain detectable together");
Check(ShredChordRules.IsExact(17,17,0),"Two-fret chord exact match");
Check(!ShredChordRules.IsExact(19,17,0),"Extra lower fret delays chord");
Check(ShredChordRules.CanArm(19,17),"Pick can arm with surplus frets");
Check(!ShredChordRules.CanArm(1,17),"Pick cannot arm missing required fret");
Check(ShredChordRules.IsExact(17,16,1),"Already sustaining fret allowed alongside next note");
Check(ShredChordRules.IsHammerPull(17,1,1,false),"Pull-off from higher fret");
Check(ShredChordRules.IsHammerPull(0,1,1,true),"Lower fret can be hammered after release");
Check(!ShredChordRules.IsHammerPull(17,16,16,false),"Releasing lower fret cannot pull off to upper fret");
var w = new ShredHitWindows(); w.SetDifficulty(5);
Check(w.ResultFor(-w.WindowFor(HitResult.Meh)).IsHit(), "Earliest 50 boundary is hittable");
Check(!w.ResultFor(-w.WindowFor(HitResult.Meh)-1).IsHit(), "Before 50 boundary is not hittable");
Check(w.ResultFor(100) < w.ResultFor(0), "Delayed judgement loses precision");
var ruleset = new ShredRuleset();
var notes = new List<HitObject> {
 new ShredHitObject{StartTime=1000,Row=0,Fret=1},
 new ShredHitObject{StartTime=1000,Row=0,Fret=3,Duration=500},
 new ShredHitObject{StartTime=1000,Row=1,Fret=5,Duration=750,Articulation=ShredPracticeArticulation.HammerPull},
 new ShredHitObject{StartTime=2000,Row=0,Fret=0,Duration=1000,Articulation=ShredPracticeArticulation.Wham},
 new ShredHitObject{StartTime=4000,Row=1,Fret=0}
};
var beatmap = new Beatmap { HitObjects = notes };
beatmap.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
beatmap.Difficulty.CircleSize=12;
beatmap.Metadata.AudioFile="audio/music.mp3";
beatmap.Metadata.Title="Audio preservation";
var writer=new StringWriter(CultureInfo.InvariantCulture);
new LegacyBeatmapEncoder(beatmap,null,null).Encode(writer);
Check(writer.ToString().Contains("AudioFilename: music.mp3"), "Patched lazer encoder preserves AudioFilename");
Check(writer.ToString().Contains("[RulesetData]"), "Patched lazer encoder writes Shred extension");
var ext=new StringWriter(CultureInfo.InvariantCulture);
ruleset.EncodeRulesetData(ext,beatmap);
var raw=notes.Cast<ShredHitObject>().Select(n => (HitObject)new RawNote{StartTime=n.StartTime,X=(float)Math.Floor((n.Column+0.5)*512/12),Duration=n.Duration}).ToArray();
beatmap.Metadata.AudioFile="music.mp3"; // Simulate the legacy basename-only metadata.
ruleset.DecodeRulesetData(beatmap,raw,ext.ToString().Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(l=>l.Trim()).ToArray());
Check(beatmap.HitObjects.Count==5,"Chord notes survive extension round trip");
Check(((ShredHitObject)beatmap.HitObjects[2]).Duration==750,"Hammer-pull sustain duration round trip");
Check(((ShredHitObject)beatmap.HitObjects[3]).IsWham,"Whammy type round trip");
Check(beatmap.Metadata.AudioFile=="audio/music.mp3", "Extension decode preserves audio metadata");
var hold=(ShredHitObject)beatmap.HitObjects[1];hold.ApplyDefaults(new ControlPointInfo(),beatmap.Difficulty);
Check(hold.NestedHitObjects.Count==1 && hold.NestedHitObjects[0].StartTime==1000,"Long note has separate onset judgement");
Check(hold.GetEndTime()==1500,"Long release time exposed to lazer");
var copy=new ShredHitObject();ruleset.CopyRulesetData(hold,copy);
Check(copy.Fret==3 && copy.Duration==500,"Editor copy preserves duration");
Reject(()=>ruleset.DecodeRulesetData(beatmap,new HitObject[]{new RawNote{X=20}},new[]{"Version:2","0,0,100"}),"Open sustain rejected");
Reject(()=>ruleset.DecodeRulesetData(beatmap,new HitObject[]{new RawNote{X=20}},new[]{"Version:2","0,4,0"}),"Zero-length wham rejected");
Reject(()=>ruleset.DecodeRulesetData(beatmap,new HitObject[]{new RawNote{X=20}},new[]{"Version:2","0,4,NaN"}),"Invalid duration rejected");
ruleset.DecodeRulesetData(beatmap,new HitObject[]{new RawCircle{X=64}},new[]{"Version:1","0,2"});
Check(((ShredHitObject)beatmap.HitObjects[0]).Articulation==ShredPracticeArticulation.HammerPull,"Version 1 pull-off maps remain readable");
Check(beatmap.Metadata.AudioFile=="audio/music.mp3","Version 1 decode leaves existing audio path untouched");
var manual = new ManualClock();
var clock = new FramedClock(manual);
void Seek(double t) { manual.CurrentTime=t; clock.ProcessFrame(); }
TestDrawable NewLong(bool wham=false)
{
    var model = new ShredHitObject { StartTime=1000,Duration=500,Fret=1 };
    if (wham) { model.Fret=0; model.Articulation=ShredPracticeArticulation.Wham; }
    model.ApplyDefaults(new ControlPointInfo(),new BeatmapDifficulty());
    var drawable = new TestDrawable(model) { Clock=clock };
    var head = new TestHead((ShredHoldHead)model.NestedHitObjects[0]) { Clock=clock };
    typeof(DrawableShredHitObject).GetField("head",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(drawable,head);
    return drawable;
}
var exact=NewLong(); Seek(1000); exact.TryHit();
Check(exact.IsHolding && !exact.Judged,"Native onset starts hold without judging tail");
Seek(1500); exact.Release();
Check(exact.Result.Type==HitResult.Perfect,"Native exact release receives Perfect");
var early=NewLong(); Seek(1000); early.TryHit(); Seek(1100); early.Release();
Check(early.Result.Type==HitResult.Miss,"Native early release breaks combo");
var late=NewLong(); Seek(1000); late.TryHit(); Seek(1800); late.CheckExpiry();
Check(late.Result.Type==HitResult.Miss,"Unreleased sustain expires as Miss");
var missed=NewLong(); Seek(1200); missed.CheckExpiry();
Check(missed.HeadJudged && !missed.HeadHit && !missed.IsHolding,"Missed onset cannot start a hold");
Seek(1500); missed.CheckExpiry();
Check(missed.Result.Type==HitResult.Miss,"Missed onset also fails release");
var gameplayBeatmap = new Beatmap<ShredHitObject>();
gameplayBeatmap.BeatmapInfo.Ruleset=ruleset.RulesetInfo;
var drawableRuleset = new DrawableShredRuleset(ruleset,gameplayBeatmap);
var field = new ShredPlayfield { Clock=clock };
typeof(ShredPlayfield).GetProperty("drawableRuleset",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(field,drawableRuleset);
var stringStates=(ShredStringState[])typeof(ShredPlayfield).GetField("strings",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(field)!;
void Judge(bool pick, int before)
 => typeof(ShredPlayfield).GetMethod("tryJudge",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(field,new object[]{0,pick,false,before});
TestDrawable AddShort(int fret,double time)
{
 var model = new ShredHitObject { StartTime=time,Fret=fret };
 model.ApplyDefaults(new ControlPointInfo(),new BeatmapDifficulty());
 var drawable = new TestDrawable(model){ Clock=clock };
 field.Add(drawable);
 return drawable;
}
var chordA=AddShort(1,3000);var chordB=AddShort(3,3000);
stringStates[0].SetFret(0,true);stringStates[0].SetFret(2,true);stringStates[0].SetFret(4,true);
Seek(3000);Judge(true,21);
Check(!chordA.Judged && !chordB.Judged,"Gameplay pick waits while surplus fret held");
stringStates[0].SetFret(4,false);Seek(3100);Judge(false,21);
Check(chordA.Result.Type==w.ResultFor(100) && chordB.Result.Type==w.ResultFor(100),"Gameplay judges every chord member at surplus release time");
var waitA=AddShort(1,4000);var waitB=AddShort(3,4000);
stringStates[0].SetFret(4,true);Seek(4000);Judge(true,21);Seek(4200);Judge(false,21);
Check(waitA.Result.Type==HitResult.Miss && waitB.Result.Type==HitResult.Miss,"Surplus frets held past 50 window miss whole chord");
var nativeScore = new ScoreProcessor(ruleset);
typeof(DrawableShredRuleset).GetProperty("scoreProcessor",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(drawableRuleset,nativeScore);
var futureA=AddShort(1,8000);var futureB=AddShort(3,8000);
nativeScore.Combo.Value=12;
int judgedBefore=nativeScore.JudgedHits;
double accuracyBefore=nativeScore.Accuracy.Value;
Seek(5000);Judge(true,21);
Check(nativeScore.Combo.Value==0 && !futureA.HeadJudged && !futureB.HeadJudged,"Distant strum breaks combo without consuming the next chord");
Judge(true,21);
Check(!futureA.HeadJudged && !futureB.HeadJudged && nativeScore.JudgedHits==judgedBefore && nativeScore.Accuracy.Value==accuracyBefore,"Repeated empty picks do not add misses, accuracy judgements, or completion events");
stringStates[0].SetFret(4,false);Seek(8000);Judge(true,5);
Check(futureA.Result.Type==HitResult.Perfect && futureB.Result.Type==HitResult.Perfect,"Chord remains playable after distant empty picks");
var near=AddShort(1,10000);
Seek(10000-near.HitObject.HitWindows.WindowFor(HitResult.Miss));Judge(true,5);
Check(near.Result.Type==HitResult.Miss,"Earliest miss boundary still judges a nearby early pick");
nativeScore.Combo.Value=6;Seek(11000);Judge(true,5);
Check(nativeScore.Combo.Value==0,"Empty pick after the last available note also breaks combo");
Check(ShredInputJudgement.IsTooEarlyToTarget(w,-w.WindowFor(HitResult.Miss)-0.01) && !ShredInputJudgement.IsTooEarlyToTarget(w,-w.WindowFor(HitResult.Miss)),"Ignore region ends exactly at the native miss boundary");
var editorModel=new EditorBeatmap(gameplayBeatmap);
var editorClock=new EditorClock(editorModel);
var grid=new ShredEditorPlayfield { RelativeSizeAxes=Axes.None, Size=new Vector2(640,500) };
// Blueprint dependencies are separate from the drawable playfield subtree.
// Reproduce loading a newly-created note with no ShredEditorPlayfield cached.
var blueprintDependencies = new osu.Framework.Allocation.DependencyContainer();
using var editorConfig = new osu.Game.Configuration.OsuConfigManager(
    new osu.Framework.Platform.NativeStorage(Path.Combine(Path.GetTempPath(), "shred-blueprint-" + Guid.NewGuid())));
blueprintDependencies.CacheAs(editorConfig);
foreach (bool isWham in new[] { false, true })
{
 var selectionNote = new ShredHitObject { Fret = 1 };
 if (isWham) { selectionNote.Fret = 0; selectionNote.Articulation = ShredPracticeArticulation.Wham; selectionNote.Duration = 500; }
 using var selectionBlueprint = new TestSelectionBlueprint(selectionNote, grid);
 blueprintDependencies.Inject(selectionBlueprint);
 Check(true, $"New {(isWham ? "wham" : "fret")} blueprint loads without a cached editor playfield");
 var editorNote = new DrawableShredEditorNote(selectionNote);
 typeof(osu.Game.Rulesets.Edit.HitObjectSelectionBlueprint).GetProperty("DrawableObject")!.SetValue(selectionBlueprint, editorNote);
 selectionBlueprint.SelectForTest();
 Check(editorNote.EditorSelected && editorNote.Alpha == 1, "Selection reaches note visual without changing hit-object alpha");
 selectionBlueprint.DeselectForTest();
 Check(!editorNote.EditorSelected, "Deselection clears note visual selection");
}
typeof(ShredEditorPlayfield).GetProperty("beatmap",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(grid,editorModel);
typeof(ShredEditorPlayfield).GetProperty("editorClock",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(grid,editorClock);
foreach(double zoom in new[]{0.5,2.0})
{
 editorModel.TimelineZoom=zoom;
 for(int row=0;row<2;row++)
 {
  double y=row==0?10:490;
  double time=grid.CurrentTime-(y-grid.Receiver(row,3).Y)/grid.Travel(row).Y*grid.VisibleTime;
  Vector2 point=grid.ToScreenSpace(grid.PositionAt(row*6+3,time));
  Check(grid.TryLocate(point,out int column,out double located) && column==row*6+3 && Math.Abs(time-located)<0.01,$"Full grid placement row {row+1}, zoom {zoom}");
 }
}
var blueprint=new ShredPlacementBlueprint(ShredPracticeArticulation.Pick);
var placed=(ShredHitObject)blueprint.HitObject;placed.StartTime=1000;placed.Fret=1;
Check(!blueprint.ReplacesExistingObject(new ShredHitObject{StartTime=1000,Fret=3}),"Editor placement preserves other frets of chord");
Check(blueprint.ReplacesExistingObject(new ShredHitObject{StartTime=1000,Fret=1}),"Editor placement replaces same fret at same time");

// v4: automatic editor lane dispatch, open zones, sweep selection and edge geometry.
for(int row=0;row<2;row++)
{
 Vector2 rightOpen=grid.Receiver(row,5)+new Vector2((grid.Skin.ColumnWidth[4]/2+20)*grid.Unit,0);
 Check(grid.TryLocate(grid.ToScreenSpace(rightOpen),out int openColumn,out double openTime) && openColumn==row*6 && Math.Abs(openTime-grid.CurrentTime)<0.01,$"Right OPEN creation zone row {row+1}");
 Vector2 futureOpen=rightOpen-grid.Travel(row)*0.8f;
 Check(grid.TryLocate(grid.ToScreenSpace(futureOpen),out openColumn,out openTime) && openColumn==row*6 && Math.Abs(openTime-grid.CurrentTime-grid.VisibleTime*0.8)<0.01,$"Right OPEN zone follows diagonal at row {row+1}");
}
Vector2 whamPoint=grid.WhamReceiver+new Vector2(50,0);
Check(grid.TryLocate(grid.ToScreenSpace(whamPoint),out int whamColumn,out double whamTime) && whamColumn==12,"Wham band is automatically detected");
Check((grid.PositionAt(12,whamTime)-whamPoint).Length<0.001,"Wham grid time and position are inverse mappings");
foreach(ShredPracticeArticulation tool in new[]{ShredPracticeArticulation.Pick,ShredPracticeArticulation.HammerPull})
{
 var placement=new ShredPlacementBlueprint(tool);
 typeof(ShredPlacementBlueprint).GetMethod("setColumn",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(placement,new object[]{12});
 Check(((ShredHitObject)placement.HitObject).IsWham,$"{tool} creates wham in wham band");
 typeof(ShredPlacementBlueprint).GetMethod("setColumn",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(placement,new object[]{8});
 var normal=(ShredHitObject)placement.HitObject;
 Check(!normal.IsWham && normal.Row==1 && normal.Fret==2 && normal.Articulation==tool,$"{tool} restored when leaving wham band");
}
Check(!ruleset.GetDefaultKeyBindings(osu.Game.Rulesets.Ruleset.EDITOR_VARIANT).Any(b=>b.KeyCombination.Keys.Contains(InputKey.Number4)),"Dedicated editor Wham shortcut removed");
Check(ruleset.GetDefaultKeyBindings().Any(b=>Convert.ToInt32(b.Action)==(int)ShredAction.Wham && b.KeyCombination.Keys.Contains(InputKey.Space)),"Gameplay wham remains on Space");
var sweep=new ShredSelectionSweep();
var firstSwept=new ShredHitObject{StartTime=1000,Fret=1};
var laterSwept=new ShredHitObject{StartTime=3000,Fret=3};
sweep.Record(new[]{firstSwept});
var preserve=new HashSet<HitObject>();sweep.AddPreservedTo(preserve);
Check(preserve.Count==0,"Before scrolling marquee can shrink normally");
sweep.PreserveAfterScroll();sweep.AddPreservedTo(preserve);
Check(preserve.Contains(firstSwept),"Scrolling retains note that left selection rectangle");
sweep.Record(new[]{laterSwept});preserve.Clear();sweep.AddPreservedTo(preserve);
Check(preserve.SetEquals(new[]{firstSwept,laterSwept}),"Successive scrolling frames accumulate visited notes");
sweep.Reset();preserve.Clear();sweep.AddPreservedTo(preserve);
Check(preserve.Count==0,"Next selection starts without previous sweep");
var cp=new ControlPointInfo();cp.Add(0,new TimingControlPoint{BeatLength=500});
int[] expectedDivisors={1,4,2,4,1};
Check(ShredGridTicks.Enumerate(cp,4,0,500).Select(t=>t.Divisor).SequenceEqual(expectedDivisors),"Quarter grid uses white blue red blue white classification");
Check(ShredGridTicks.Enumerate(cp,8,0,500).Single(t=>Math.Abs(t.Time-62.5)<0.001).Divisor==8,"Wham eighth subdivisions use lazer's yellow category");
Check(ShredGridTicks.Enumerate(cp,16,0,500).Single(t=>Math.Abs(t.Time-31.25)<0.001).Divisor==16,"Wham sixteenth subdivisions use lazer's purple category");
cp.Add(300,new TimingControlPoint{BeatLength=400});
var changedTicks=ShredGridTicks.Enumerate(cp,4,0,600).ToArray();
Check(changedTicks.Any(t=>t.Time==300 && t.Divisor==1) && !changedTicks.Any(t=>t.Time==375),"Frets and wham reset the grid at a timing change");
field.RelativeSizeAxes=Axes.None;field.Size=new Vector2(1000,720);
float fretRightAtMiddle=(field.Receiver(0,5).X+field.Receiver(1,5).X)/2+field.Skin.ColumnWidth[4]*field.Unit/2;
Check(Math.Abs(field.WhamLineCentre.X-field.WhamLineWidth/2-fretRightAtMiddle-field.Skin.ColumnSpacing[3]*field.Unit)<0.001,"Wham judgement line uses fret-to-fret spacing");
Check(Math.Abs(grid.WhamReceiver.X-grid.WhamLineCentre.X-grid.WhamLineWidth/2)<0.001,"Editor wham onset uses the visible contact face of the line");
var wham=NewLong(true);Seek(1000);wham.TryHit();
DrawableShredHoldHead whamHead=(DrawableShredHoldHead)typeof(DrawableShredHitObject).GetField("head",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(wham)!;
Check(whamHead.Result.Type==HitResult.Perfect,"Wham exact first-edge time receives Perfect onset");
Check((field.PositionAt(wham.HitObject,1000)-field.WhamReceiver).Length<0.001,"Wham first edge reaches judgement at StartTime");
Seek(1500);wham.Release();
Check(wham.Result.Type==HitResult.Perfect && (field.PositionAt(wham.HitObject,1500)-field.WhamReceiver).Length<0.001,"Wham last edge reaches judgement at perfect release time");
var visualParent=new Container{Size=new Vector2(8,10)};
var bar=new ShredNoteVisual(wham.HitObject,new ShredSkinSettings());visualParent.Add(bar);
bar.Layout(new Vector2(100,0),1);
Box body=(Box)typeof(ShredNoteVisual).GetField("body",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(bar)!;
ShredGlyph headGlyph=(ShredGlyph)typeof(ShredNoteVisual).GetField("head",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(bar)!;
Check(headGlyph.Alpha==0 && Math.Abs(body.Alpha-ShredWhamGeometry.Opacity)<0.001,"Wham is uniformly translucent with no bright head");
Check(body.Size.X==100 && body.Position.X-body.Size.X/2==0 && body.Position.X+body.Size.X/2==100,"Wham body extends exactly from first to last edge without head padding");
Box grip=(Box)typeof(ShredNoteVisual).GetField("hook",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(bar)!;
Check(grip.Position.X==100-ShredWhamGeometry.HookInset && grip.Position.Y==0,"Wham grip begins inside the bar to join its silhouette");
Check(ShredWhamGeometry.ContainsVisibleBar(new Vector2(50,0),Vector2.Zero,new Vector2(100,0),10,0.2f,1),"Right-click hit area covers the middle of a wham");
Check(ShredWhamGeometry.ContainsVisibleBar(new Vector2(112,7),Vector2.Zero,new Vector2(100,0),10,0.2f,1),"Right-click hit area covers the angled wham grip");
Check(!ShredWhamGeometry.ContainsVisibleBar(new Vector2(50,12),Vector2.Zero,new Vector2(100,0),10,0.2f,1),"Right-click hit area excludes space outside the visible wham");
var solidNote=new ShredNoteVisual(new ShredHitObject{Fret=3,Duration=500},new ShredSkinSettings()){RelativeSizeAxes=Axes.None,Size=new Vector2(40,12)};
solidNote.Layout(new Vector2(0,100),1);
ShredGlyph solidHead=(ShredGlyph)typeof(ShredNoteVisual).GetField("head",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(solidNote)!;
ShredGlyph solidTail=(ShredGlyph)typeof(ShredNoteVisual).GetField("tail",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(solidNote)!;
Box GlyphFill(ShredGlyph glyph)=>(Box)typeof(ShredGlyph).GetField("fill",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(glyph)!;
var receptor=new ShredGlyph(osuTK.Graphics.Color4.White);
Check(GlyphFill(solidHead).Alpha==1 && GlyphFill(solidTail).Alpha==1 && GlyphFill(receptor).Alpha<1,"Note heads and tails are solid while receptors remain hollow");
var originalColour=solidHead.Colour;
solidNote.Layout(new Vector2(0,100),1,selected:true);
Check(!solidHead.Colour.Equals(originalColour) && solidHead.Colour.Equals(solidTail.Colour) && solidNote.Alpha==1,"Selection lightens both ends of a sustain");
solidNote.Layout(new Vector2(0,100),1,selected:false);
Check(solidHead.Colour.Equals(originalColour),"Deselecting restores the note colour");
var openVisual=new ShredNoteVisual(new ShredHitObject{Fret=0},new ShredSkinSettings());
openVisual.Layout(Vector2.Zero,1,selected:true);
Check(openVisual.Alpha<1,"White OPEN selection remains visible through a visual fade");
openVisual.Layout(Vector2.Zero,1);
Check(openVisual.Alpha==1,"OPEN deselection restores opacity");
var geometryField=new ShredPlayfield{RelativeSizeAxes=Axes.None,Size=new Vector2(1280,720),Clock=clock};
Check(Math.Abs(geometryField.Unit-1.3f)<0.0001 && Math.Abs(geometryField.NoteSize(1).X-geometryField.Skin.ColumnWidth[0]*1.3f)<0.0001,"Gameplay receptors and notes are enlarged by 30 percent");
Check(Math.Abs(grid.Unit-1.3f)<0.0001,"Editor playfield is enlarged by the same 30 percent");
for(int row=0;row<2;row++)
for(int fret=1;fret<=5;fret++)
{
 Vector2 edge=geometryField.Receiver(row,fret)-geometryField.LaneTravel(row,fret);
 Check(Math.Min(Math.Min(Math.Abs(edge.X),Math.Abs(edge.X-geometryField.DrawWidth)),Math.Min(Math.Abs(edge.Y),Math.Abs(edge.Y-geometryField.DrawHeight)))<0.001,$"Lane {row}/{fret} reaches a viewport edge");
}
Check(Math.Abs(geometryField.Travel(0).Length-geometryField.Skin.ScrollLength*geometryField.MovementUnit)<0.001,"Extended lanes preserve the previous note movement speed");
var visibleModel=new ShredHitObject{StartTime=12000,Fret=3};visibleModel.ApplyDefaults(new ControlPointInfo(),new BeatmapDifficulty());
var visibleNote=new TestDrawable(visibleModel){Clock=clock};
typeof(DrawableShredHitObject).GetProperty("field",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(visibleNote,geometryField);
Seek(12000-ShredPlayfield.ApproachTime*1.5);visibleNote.UpdateForTest();
Check(visibleNote.Alpha==1 && visibleNote.Position.X>0 && visibleNote.Position.Y>0 && visibleNote.Position.Y<geometryField.DrawHeight,"Note is visible on screen before the old appearance cutoff");
var replayMap=new Beatmap<ShredHitObject>();replayMap.BeatmapInfo.Ruleset=ruleset.RulesetInfo;
foreach(double time in new[]{1000d,1200d})
{
 var replayNote=new ShredHitObject{StartTime=time,Fret=1};
 replayNote.ApplyDefaults(new ControlPointInfo(),new BeatmapDifficulty());replayMap.HitObjects.Add(replayNote);
}
var replayScore=new ScoreProcessor(ruleset);replayScore.ApplyBeatmap(replayMap);
var replayRuleset=new DrawableShredRuleset(ruleset,replayMap){Clock=clock};
typeof(DrawableShredRuleset).GetProperty("scoreProcessor",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(replayRuleset,replayScore);
replayRuleset.NewResult+=replayScore.ApplyResult;replayRuleset.RevertResult+=replayScore.RevertResult;
var nativePlayfield=replayRuleset.Playfield;
var emit=(Action<DrawableHitObject,osu.Game.Rulesets.Judgements.JudgementResult>)typeof(osu.Game.Rulesets.UI.Playfield).GetField("NewResult",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(nativePlayfield)!;
var revert=(Action<osu.Game.Rulesets.Judgements.JudgementResult>)typeof(osu.Game.Rulesets.UI.Playfield).GetField("RevertResult",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(nativePlayfield)!;
var resultA=new osu.Game.Rulesets.Judgements.JudgementResult(replayMap.HitObjects[0],replayMap.HitObjects[0].Judgement){Type=HitResult.Perfect};
var resultB=new osu.Game.Rulesets.Judgements.JudgementResult(replayMap.HitObjects[1],replayMap.HitObjects[1].Judgement){Type=HitResult.Perfect};
Seek(1000);emit(null!,resultA);Seek(1100);replayRuleset.RecordEmptyPick(1100);Seek(1200);emit(null!,resultB);
Check(replayScore.Combo.Value==1 && replayScore.JudgedHits==2 && replayScore.Accuracy.Value==1,"Native result forwarding preserves completion count and accuracy across an empty strum");
Seek(1050);revert(resultB);
typeof(DrawableShredRuleset).GetMethod("UpdateAfterChildren",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(replayRuleset,null);
Check(replayScore.Combo.Value==1 && replayScore.JudgedHits==1,"Rewinding just before an empty strum restores the earlier combo");
Seek(1100);replayRuleset.RecordEmptyPick(1100);Seek(1200);emit(null!,resultB);
Seek(900);revert(resultB);revert(resultA);
Check(replayScore.Combo.Value==0 && replayScore.JudgedHits==0,"Rewinding across notes and an empty strum restores results in order");
Console.WriteLine($"{count} checks passed.");
partial class TestSelectionBlueprint : ShredSelectionBlueprint
{
 public void SelectForTest() { Select(); OnSelected(); }
 public void DeselectForTest() { Deselect(); OnDeselected(); }
 public TestSelectionBlueprint(ShredHitObject note, ShredEditorPlayfield playfield) : base(note, playfield)
 {
  // This regression exercises dependency injection, without a renderer.
  ClearInternal();
 }
}
class RawNote : HitObject,IHasXPosition,IHasDuration { public float X{get;set;} public double Duration{get;set;} public double EndTime=>StartTime+Duration; }

class RawCircle : HitObject,IHasXPosition { public float X{get;set;} }

class TestDrawable : DrawableShredHitObject
{
 public void UpdateForTest() => base.Update();
 public TestDrawable(ShredHitObject h) : base(h) {}
 public override void PlaySamples() {}
 protected override void UpdateHitStateTransforms(ArmedState state) {}
}
class TestHead : DrawableShredHoldHead
{
 public TestHead(ShredHoldHead h) : base(h) {}
 public override void PlaySamples() {}
}
