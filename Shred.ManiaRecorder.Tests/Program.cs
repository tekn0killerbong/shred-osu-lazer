using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Edit;
using osuTK.Input;

static void Check(bool valid, string reason) { if (!valid) throw new Exception(reason); }
int scenarios = 0;
foreach (int variant in new ManiaRuleset().GameplayVariants)
{
    int count = variant;
    if (variant >= (int)PlayfieldType.Dual) count -= (int)PlayfieldType.Dual;
    var columns = Enum.GetValues<Key>().SelectMany(key => ManiaStepRecorder.ColumnsFor(variant, count, key)).Distinct().Order().ToArray();
    Check(columns.SequenceEqual(Enumerable.Range(0, count)), $"Unreachable column, variant {variant}");
    Key empty = ManiaStepRecorder.EmptyStepKey(count, Key.Slash);
    Check(ManiaStepRecorder.ColumnsFor(variant, count, empty).Length == 0, "Empty key conflicts with note");
    Check(empty == (count % 2 == 1 ? Key.Slash : Key.Space), "Wrong parity");
    if (count % 2 == 1) Check(ManiaStepRecorder.ColumnsFor(variant, count, Key.Space).SequenceEqual(new[] { count / 2 }), "Space must play the default central column");
    var notes = new List<ManiaStepRecorderState.Note>();
    double cursor = 0;
    var recorder = new ManiaStepRecorderState(1000, p => p + 125, notes.Add, _ => { }, p => cursor = p);
    for (int c = 0; c < count; c++) recorder.Press(c, 0);
    for (int c = 0; c < count - 1; c++) { recorder.Release(c, 100); Check(recorder.Position == 1000, "Early chord advance"); }
    recorder.Release(count - 1, 100);
    Check(recorder.Position == 1125 && notes.Count == count, "Chord should advance once");
    Check(notes.All(n => !n.Sustain), "Short notes became holds");
    notes.Clear(); recorder.Finish();
    for (int c = 0; c < count; c++) recorder.Press(c, 1000);
    recorder.Tick(2000);
    Check(notes.All(n => n.Sustain), "Every mania column must support LN, including 0/6/12/18");
    Check(recorder.Position == 1125, "Threshold must not advance");
    recorder.EmptyStep(2001);
    Check(notes.All(n => n.End == 1250) && cursor == 1250, "Empty step must extend every hold");
    recorder.Navigate(1500, 2010); Check(notes.All(n => n.End == 1500), "Forward tail adjustment");
    recorder.Navigate(500, 2020); Check(notes.All(n => n.End == n.Start), "Tail must clamp to head");
    recorder.Navigate(1625, 2030); Check(notes.All(n => n.End == 1625), "Hold cannot extend after backwards navigation");
    for (int c = 0; c < count; c++) recorder.Release(c, 2050);
    Check(recorder.Position == 1625, "LN release should not add a step");
    Console.WriteLine($"PASS mania variant {variant}: {count} columns, defaults, empty key, chord release, LN threshold, tail navigation"); scenarios++;
}
{
    var notes = new List<ManiaStepRecorderState.Note>();
    var recorder = new ManiaStepRecorderState(0, p => p + 100, notes.Add, _ => { }, _ => { });
    recorder.Press(0, 0); recorder.Press(0, 1); Check(notes.Count == 1, "Repeat creates duplicate");
    recorder.Tick(1000); recorder.Press(1, 1001); recorder.Release(1, 1010);
    Check(recorder.Position == 100 && notes[0].End == 100, "Short release should extend held LN");
    recorder.Finish(); recorder.Navigate(800, 1100); recorder.Press(1, 1110); recorder.Release(1, 1120);
    Check(recorder.Position == 900, "Navigation blocks auto step");
    Console.WriteLine("PASS mixed held LN, duplicate edge and auto-step after navigation"); scenarios++;
}
Console.WriteLine($"PASS TOTAL {scenarios} mania recorder scenarios");
