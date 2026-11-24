using System.Text;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Beatmaps;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Gameplay;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;
if (args.Length > 0 && args[0] == "--v70-editor") { Revision54EditorRateChecks.Run(revision70: true); return; }
if (args.Length > 0 && args[0] == "--v69-gameplay") { GameplayChecks.RunRevision69(); return; }
if (args.Length > 0 && args[0] == "--v68-snap") { Revision68SnapChecks.Run(); return; }
if (args.Length > 0 && args[0] == "--v68-gameplay") { GameplayChecks.RunRevision68(); return; }
if (args.Length > 0 && args[0] == "--v68-editor") { Revision54EditorRateChecks.Run(revision68: true); return; }
if (args.Length > 0 && args[0] == "--v67-editor") { Revision54EditorRateChecks.Run(revision67: true); return; }
if (args.Length > 0 && args[0] == "--v66-editor") { Revision54EditorRateChecks.Run(revision66: true); return; }
if (args.Length > 0 && args[0] == "--v65-editor") { Revision54EditorRateChecks.Run(revision65: true); return; }
if (args.Length > 0 && args[0] == "--v64-editor") { Revision54EditorRateChecks.Run(revision64: true); return; }
if (args.Length > 0 && args[0] == "--v63-audio") { Revision63AudioChecks.Run(); return; }
if (args.Length > 0 && args[0] == "--v63-editor-audio") { Revision54EditorRateChecks.Run(revision63: true); return; }
if (args.Length > 0 && args[0] == "--v62-bindings") { Revision54EditorRateChecks.Run(revision62: true); return; }
if (args.Length > 0 && args[0] == "--v61-snap") { Revision61SnapChecks.Run(); return; }
if (args.Length > 0 && args[0] == "--v61-editor") { Revision54EditorRateChecks.Run(revision61: true); return; }
if (args.Length > 0 && args[0] == "--v61-gameplay") { GameplayChecks.RunRevision61(); return; }
if (args.Length > 0 && args[0] == "--v60-map") { GameplayChecks.RunRevision60Map(args[1]); return; }
if (args.Length > 0 && args[0] == "--v60-editor") { Revision54EditorRateChecks.Run(revision60: true); return; }
if (args.Length > 0 && args[0] == "--v60-gameplay") { GameplayChecks.RunRevision60(); return; }
if (args.Length > 0 && args[0] == "--v59-compose") { Revision54EditorRateChecks.Run(revision59: true, composeOnly: true); return; }
if (args.Length > 0 && args[0] == "--v59-editor") { Revision54EditorRateChecks.Run(revision59: true); return; }
if (args.Length > 0 && args[0] == "--v58-editor") { Revision54EditorRateChecks.Run(revision58: true); return; }
if (args.Length > 0 && args[0] == "--v58-scroll") { GameplayChecks.RunRevision58Scroll(); return; }
if (args.Length > 0 && (args[0] == "--v58-hammer-pulls" || args[0] == "--v57-hammer-pulls")) { GameplayChecks.RunRevision58HammerPulls(); return; }
if (args.Length > 0 && args[0] == "--v57-editor") { Revision54EditorRateChecks.Run(revision57: true, persistenceFolder: args[1], restart: args.Contains("--restart")); return; }
if (args.Length > 0 && args[0] == "--v56-timing") { Revision54EditorRateChecks.Run(timingOnly: true); return; }
if (args.Length > 0 && args[0] == "--v56-compose") { Revision54EditorRateChecks.Run(composeOnly: true); return; }
if (args.Length > 0 && args[0] == "--editor-v55-history") { EditorUiProbe.Revision55Only = true; EditorUiProbe.Run(args[1], args[2]); return; }
if (args.Length > 0 && args[0] == "--v54-wham-glow") { Revision54WhamGrindChecks.Run(); return; }
if (args.Length > 0 && args[0] == "--v54-grid-memory") { Revision54GridMemoryChecks.Run(args[1], args[2], args.Contains("--restart")); return; }
if (args.Length > 0 && args[0] == "--v54-whams") { GameplayChecks.RunRevision54Whams(); return; }
if (args.Length > 0 && args[0] == "--editor-v54") { EditorUiProbe.Revision54Only = true; EditorUiProbe.Run(args[1], args[2]); return; }
if (args.Length > 0 && args[0] == "--v54-editor-rate") { Revision54EditorRateChecks.Run(); return; }
if (args.Length > 0 && args[0] == "--v54-editor-creation") { Revision54EditorRateChecks.Run(creationOnly: true); return; }
if (args.Length > 0 && args[0] == "--v53-logo-filter") { Revision53LogoFilteringChecks.Run(args.Skip(1).FirstOrDefault()); return; }
if (args.Length > 0 && args[0] == "--v53-gameplay") { GameplayChecks.RunRevision53(); return; }
if (args.Length > 0 && args[0] == "--v52-gameplay") { GameplayChecks.RunRevision50(); GameplayChecks.RunRevision52(); return; }
if (args.Length > 0 && args[0] == "--v51-logo") { Revision51LogoChecks.Run(); return; }
if (args.Length > 0 && args[0] == "--v51-pick-audit") { GameplayChecks.RunLegacy20Audit(); GameplayChecks.RunLegacy21Audit(); GameplayChecks.RunLegacy22Audit(); GameplayChecks.RunLegacyPickAudit(); return; }
if (args.Length > 0 && args[0] == "--v51-history") { Revision51HistoryChecks.Run(); return; }
if (args.Length > 0 && args[0] == "--editor-v51") { EditorUiProbe.Revision51Only=true; EditorUiProbe.Run(args[1],args[2]); return; }
if (args.Length > 0 && args[0] == "--editor-v50") { EditorUiProbe.Revision50Only=true; EditorUiProbe.Run(args[1],args[2]); return; }
if (args.Length > 0 && args[0] == "--v50-history") { Revision50HistoryChecks.Run(); return; }
if (args.Length > 0 && args[0] == "--v50-gameplay") { GameplayChecks.RunRevision50(); return; }
if (args.Length > 0 && args[0] == "--v49") { Revision49Checks.Run(args[1],args[2]); return; }
if (args.Length > 0 && args[0] == "--editor-guitars") { EditorUiProbe.Revision49Only = true; EditorUiProbe.Run(args[1],args[2]); return; }
if (args.Length > 0 && args[0] == "--gameplay-guitars") { EditorUiProbe.TestGameplayMode = true; EditorUiProbe.TestAudioMode = true; EditorUiProbe.TestSkinArchive = Revision49Checks.CreateConflictingSkin(); EditorUiProbe.TestStartTime = 19000; EditorUiProbe.Run(args[1],args[2]); return; }
if (args.Length > 0 && args[0] == "--editor-pointer") { EditorUiProbe.Revision48Only = true; EditorUiProbe.Run(args[1],args[2]); return; }
if (args.Length > 0 && args[0] == "--v47") { Revision47Checks.Run(); return; }

if (args.Length > 0 && args[0] == "--editor-diagnose") { EditorUiProbe.Revision47Only = true; if(args.Length>3)EditorUiProbe.TestSkinArchive=args[3]; EditorUiProbe.Run(args[1], args[2]); return; }

if (args.Length > 0 && args[0] == "--editor-samples") { EditorUiProbe.Revision46Only = true; EditorUiProbe.Run(args[1], args[2]); return; }
if (args.Length > 0 && args[0] == "--v46") { Revision46Checks.Run(); return; }

if (args.Length > 0 && args[0] == "--export-ui") { EditorUiProbe.ExportDirectory=args[3]; EditorUiProbe.Run(args[1],args[2]); return; }
if (args.Length > 0 && args[0] == "--actual-map") { GameplayChecks.ProbeActualMap(args[1], args.Length>2); return; }
if (args.Length > 0 && args[0] == "--transition-probe") { GameplayChecks.ProbeTransitions(); return; }
if (args.Length > 0 && args[0] == "--editor-test") { EditorUiProbe.TestGameplayMode = true; if(args.Length > 3) EditorUiProbe.TestStartTime = double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture); EditorUiProbe.Run(args[1], args[2]); return; }
if (args.Length > 0 && args[0] == "--gameplay-audio") { EditorUiProbe.TestGameplayMode = true; EditorUiProbe.TestAudioMode = true; EditorUiProbe.TestStartTime = 19000; EditorUiProbe.Run(args[1], args[2]); return; }
if (args.Length > 0 && args[0] == "--editor-audio") { EditorUiProbe.Revision45Only = true; EditorUiProbe.Run(args[1], args.Length > 2 ? args[2] : "riff"); return; }
if (args.Length > 0 && args[0] == "--v45") { Revision45Checks.Run(); return; }
if (args.Length > 0 && args[0] == "--editor-ui") { EditorUiProbe.Run(args[1], args.Length > 2 ? args[2] : "riff"); return; }
var shred = new ShredRuleset();
using var store = new Store(shred.RulesetInfo);
Decoder.RegisterDependencies(store);
string map = """
shred portable format v1
[General]
AudioFilename: music.wav
Mode:3
CustomRuleset:shred
[Metadata]
Title:Portable test
Artist:shred
Creator:test
Version:shred test
BeatmapID:123
BeatmapSetID:456
[Difficulty]
CircleSize:12
OverallDifficulty:5
HPDrainRate:5
SliderMultiplier:1.4
SliderTickRate:1
[TimingPoints]
0,500,4,1,0,100,1,0
[HitObjects]
149,192,2000,1,0,0:0:0:0:
362,192,1000,1,0,0:0:0:0:
447,192,1000,1,0,0:0:0:0:
21,192,3000,1,0,0:0:0:0:
[RulesetData]
Version:2
AudioFile:bXVzaWMud2F2
0,1,500
1,0,0
2,0,0
3,4,750
""";
Beatmap Read(string text) { using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(text)); using var reader = new LineBufferedReader(bytes); return Decoder.GetDecoder<Beatmap>(reader).Decode(reader); }
void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
var decoded=Read(map);
var direct=Read(map.Replace(ShredPortableDecoder.Magic,"osu file format v128"));
Check(direct.HitObjects.Cast<ShredCarrierNote>().Select(n=>n.ToShred()).Zip(decoded.HitObjects.Cast<ShredCarrierNote>().Select(n=>n.ToShred())).All(p=>p.First.Row==p.Second.Row && p.First.Fret==p.Second.Fret && p.First.StartTime==p.Second.StartTime && p.First.Duration==p.Second.Duration && p.First.Articulation==p.Second.Articulation), "Direct native preview export preserves all shred data without script");
Check(Read(map.Replace("Version:2", "Version:2 [shred v29]")).HitObjects.Count==4, "Recover internal version damaged by old conversion script");
Check(decoded.BeatmapInfo.Ruleset.OnlineID==0, "Carrier resolves standard so song select permits converted rulesets");
string withVelocity = map.Replace("0,500,4,1,0,100,1,0", "0,500,4,1,0,100,1,0\n1000,-50,4,1,0,100,0,1");
Check(Read(withVelocity).ControlPointInfo.EffectPointAt(1500).ScrollSpeed == 2, "Portable Shred import preserves effect scroll speed");
Check(Read(withVelocity.Replace(ShredPortableDecoder.Magic, "osu file format v128")).ControlPointInfo.EffectPointAt(1500).ScrollSpeed == 2, "Developer Shred export preserves effect scroll speed on official import");
Check(decoded.HitObjects.Count==4 && decoded.HitObjects.All(n=>n is ShredCarrierNote), "Standard-compatible carriers decoded by dedicated magic");
var notes=decoded.HitObjects.Cast<ShredCarrierNote>().Select(n=>n.ToShred()).ToArray();
Check(notes[0].Row==1 && notes[0].Fret==2 && notes[1].Row==1 && notes[1].Fret==4, "Two-row chord columns preserved");
Check(notes[2].StartTime==2000 && notes[2].Articulation==ShredPracticeArticulation.HammerPull && notes[2].Duration==500, "Unsorted source indices preserve articulation and hold duration");
Check(notes[3].IsWham && notes[3].Duration==750, "Wham data preserved");
Check(decoded.Metadata.AudioFile=="music.wav", "Audio resource retained");
Check(decoded.BeatmapInfo.OnlineID<=0 && decoded.BeatmapInfo.BeatmapSet.OnlineID<=0, "Converted maps do not retain official online IDs");
string nativeHolds = map.Replace("2000,1,0,0:0:0:0:", "2000,128,0,2500:0:0:0:0:")
    .Replace("1000,1,0,0:0:0:0:", "1000,128,0,1000:0:0:0:0:")
    .Replace("3000,1,0,0:0:0:0:", "3000,128,0,3750:0:0:0:0:");
Check(Read(nativeHolds).HitObjects.Cast<ShredCarrierNote>().Select(n=>n.ToShred()).Zip(notes).All(p=>p.First.Row==p.Second.Row && p.First.Fret==p.Second.Fret && p.First.Duration==p.Second.Duration && p.First.Articulation==p.Second.Articulation), "Real preview hold-style exports retain all native data with standard carrier");
var converter=shred.CreateBeatmapConverter(decoded);
Check(converter.CanConvert(), "Official carrier accepted by shred converter");
var converted=converter.Convert();
Check(converted.HitObjects.Cast<ShredHitObject>().Zip(notes).All(p=>p.First.Duration==p.Second.Duration && p.First.Articulation==p.Second.Articulation && p.First.Row==p.Second.Row && p.First.Fret==p.Second.Fret && !ReferenceEquals(p.First,p.Second)), "Gameplay conversion preserves data without shared mutable notes");
string vanilla=map.Substring(0,map.IndexOf("[RulesetData]",StringComparison.Ordinal)).Replace(ShredPortableDecoder.Magic,"osu file format v128").Replace("CustomRuleset:shred","");
Check(Read(vanilla).HitObjects.All(n=>n is not ShredHitObject && n is not ShredCarrierNote), "Unmarked maps remain decoded as official objects");
foreach (int version in new[]{3,14,128})
foreach (int mode in new[]{0,1,2,3})
{
    string legacyText=vanilla.Replace("osu file format v128", "osu file format v"+version).Replace("Mode:3", "Mode:"+mode);
    using var legacyBytes=new MemoryStream(Encoding.UTF8.GetBytes(legacyText));
    using var legacyReader=new LineBufferedReader(legacyBytes);
    var reference=new LegacyBeatmapDecoder(version).Decode(legacyReader);
    var routed=Read(legacyText);
    Check(routed.BeatmapInfo.Ruleset.ShortName==reference.BeatmapInfo.Ruleset.ShortName && routed.BeatmapInfo.DifficultyName==reference.BeatmapInfo.DifficultyName && routed.Difficulty.CircleSize==reference.Difficulty.CircleSize && routed.HitObjects.Count==reference.HitObjects.Count && routed.HitObjects.Zip(reference.HitObjects).All(p=>p.First.GetType()==p.Second.GetType() && p.First.StartTime==p.Second.StartTime), "Unmarked official mode "+mode+" format v"+version+" matches original decoder");
}
Check(!Read(vanilla).BeatmapInfo.AllowGameplayWithRuleset(shred.RulesetInfo, true), "Regression: official song select rejects the former mania carrier");
Check(decoded.BeatmapInfo.AllowGameplayWithRuleset(shred.RulesetInfo, true), "Official song select accepts corrected carrier when converted maps are enabled");
Check(!decoded.BeatmapInfo.AllowGameplayWithRuleset(shred.RulesetInfo, false), "Converted-map filter must be enabled");

Check(shred.CreateEditorSetupSections().Any(s => s is osu.Game.Rulesets.Shred.Edit.ShredDifficultySection), "Official Shred difficulty setup exposed");
var ordinary = Read(vanilla.Replace("Mode:3", "Mode:0").Replace("CircleSize:12", "CircleSize:4"));
var ordinaryWorking = new FlatWorkingBeatmap(ordinary);
var ordinaryPlayable = ordinaryWorking.GetPlayableBeatmap(shred.RulesetInfo);
Check(ordinaryPlayable.HitObjects.Count == ordinary.HitObjects.Count && ordinaryPlayable.HitObjects.Cast<ShredHitObject>().All(n=>n.Row is 0 or 1 && n.Fret >= 1 && n.Fret <= 5), "Song-select WorkingBeatmap statistics path accepts ordinary standard maps");
Check(double.IsFinite(shred.CreateDifficultyCalculator(ordinaryWorking).Calculate().StarRating), "Ordinary standard conversion difficulty calculation succeeds");
var geometry = new Beatmap { HitObjects = new()
{
    new osu.Game.Rulesets.Osu.Objects.HitCircle { StartTime=1000, Position=new osuTK.Vector2(-10, 0) },
    new osu.Game.Rulesets.Osu.Objects.Spinner { StartTime=2000, Duration=500, Position=new osuTK.Vector2(600, 384) }
}};
var geometryNotes = shred.CreateBeatmapConverter(geometry).Convert().HitObjects.Cast<ShredHitObject>().ToArray();
Check(geometryNotes[0].Row==0 && geometryNotes[0].Fret==1 && geometryNotes[1].Row==1 && geometryNotes[1].Fret==5 && geometryNotes[1].Duration==500, "Standard conversion handles both rows, edge positions and explicit durations");
Check(shred.CreateHitObjectComposer() is osu.Game.Rulesets.Shred.Edit.ShredHitObjectComposer, "Official Shred composer exposed");
EditorCompatibilityChecks.Run(decoded);
EditorCompatibilityChecks.RunMatrix();
RecorderTailChecks.Run();
Check(typeof(ShredRuleset).GetInterfaces().All(t=>t.Name!="ILegacyBeatmapExtension") && typeof(ShredHitObject).GetInterfaces().All(t=>t.Name!="IHasTimingDistribution"), "No fork-only interface dependencies");
try {Read(map.Replace("3,4,750","3,4,-1"));throw new Exception("Invalid duration accepted");}catch(FormatException){Console.WriteLine("PASS Invalid native data rejected");}
var osu = new osu.Game.Rulesets.Osu.OsuRuleset();
foreach (string input in new[]{map, nativeHolds, nativeHolds.Replace(ShredPortableDecoder.Magic,"osu file format v128"), nativeHolds.Replace("Version:2", "Version:2 [shred v29]")})
{
    var working = new FlatWorkingBeatmap(Read(input));
    var standard = working.GetPlayableBeatmap(osu.RulesetInfo);
    Check(standard.HitObjects.Count == 4, "Actual official standard converter accepts all carrier objects");
    var stars = osu.CreateDifficultyCalculator(working).Calculate().StarRating;
    Check(double.IsFinite(stars), "Import-time official standard difficulty calculation succeeds");
    working.BeatmapInfo.UpdateStatisticsFromBeatmap(working.Beatmap);
    var playable = working.GetPlayableBeatmap(shred.RulesetInfo);
    Check(playable.HitObjects.Cast<ShredHitObject>().Zip(notes).All(p=>p.First.StartTime==p.Second.StartTime && p.First.Duration==p.Second.Duration && p.First.Row==p.Second.Row && p.First.Fret==p.Second.Fret && p.First.Articulation==p.Second.Articulation), "Full WorkingBeatmap path recovers native shred notes after standard calculation");
    Check(working.Beatmap.HitObjects.OfType<osu.Game.Rulesets.Objects.Types.IHasDuration>().Max(n=>n.EndTime)==3750, "Carrier retains final sustain end time");
}
var oldMap = Read(map);
oldMap.HitObjects = oldMap.HitObjects.Cast<ShredCarrierNote>().Select(n=>(osu.Game.Rulesets.Objects.HitObject)n.ToShred()).ToList();
osu.CreateDifficultyCalculator(new FlatWorkingBeatmap(oldMap)).Calculate();
Console.WriteLine("PASS Official native objects now also expose standard carrier geometry");
foreach (string archive in args)
{
    using var zip = System.IO.Compression.ZipFile.OpenRead(archive);
    foreach (var entry in zip.Entries.Where(e=>e.FullName.EndsWith(".osu",StringComparison.OrdinalIgnoreCase)))
    {
        using var stream=entry.Open();
        using var reader=new LineBufferedReader(stream);
        var imported=Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
        NativeIdentityChecks.Run(imported);
        var expected=imported.HitObjects.Cast<ShredCarrierNote>().Select(n=>n.ToShred()).ToArray();
        EditorCompatibilityChecks.Run(imported);
        var working=new FlatWorkingBeatmap(imported);
        Check(double.IsFinite(osu.CreateDifficultyCalculator(working).Calculate().StarRating), "Actual shipped OSZ: official difficulty calculation succeeds");
        working.BeatmapInfo.UpdateStatisticsFromBeatmap(working.Beatmap);
        var playable=working.GetPlayableBeatmap(shred.RulesetInfo);
        Check(playable.HitObjects.Count==expected.Length && playable.HitObjects.Cast<ShredHitObject>().Zip(expected).All(p=>p.First.StartTime==p.Second.StartTime && p.First.Duration==p.Second.Duration && p.First.Row==p.Second.Row && p.First.Fret==p.Second.Fret && p.First.Articulation==p.Second.Articulation), "Actual shipped OSZ: shred data survives complete conversion path");
    }
}
Console.WriteLine("API " + shred.RulesetAPIVersionSupported);
Revision39Checks.Run();
Revision41VisualChecks.Run();
GameplayChecks.Run();
GameplayChecks.RunRevision40();
GameplayChecks.RunRevision43();
KeyboardChecks.Run();
ScrollBindingChecks.Run();
Revision45Checks.Run();
Revision46Checks.Run();
NativeIdentityChecks.Run(Read(map));
sealed class Store(RulesetInfo shred) : RulesetStore
{
 public override IEnumerable<RulesetInfo> AvailableRulesets => new[]{new RulesetInfo{ShortName="taiko",Name="taiko",OnlineID=1,Available=true,InstantiationInfo=shred.InstantiationInfo},new RulesetInfo{ShortName="fruits",Name="catch",OnlineID=2,Available=true,InstantiationInfo=shred.InstantiationInfo},shred,new osu.Game.Rulesets.Osu.OsuRuleset().RulesetInfo,new RulesetInfo{ShortName="mania",Name="mania",OnlineID=3,Available=true,InstantiationInfo=shred.InstantiationInfo}};
}
