using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Scoring;
using osu.Game.Rulesets.Shred.Skinning;
using osu.Game.Rulesets.Shred.UI;
using osu.Game.Screens.Edit;
using osuTK;

internal static partial class Program
{
    private static void RunRevision26Tests()
    {
        var cases = new (string name, Action run)[]
        {
            ("Recorder on/off and return survive reopening the settings file", () =>
            {
                using var storage = new TemporaryNativeStorage("shred-recorder-v26");
                foreach (bool on in new[] { true, false })
                {
                    using (var config = new ShredRecorderConfig(storage))
                    {
                        config.SetValue(ShredRecorderSetting.Enabled, on);
                        config.SetValue(ShredRecorderSetting.ReturnOnPause, !on);
                    }
                    using var reopened = new ShredRecorderConfig(storage);
                    check(reopened.Get<bool>(ShredRecorderSetting.Enabled) == on);
                    check(reopened.Get<bool>(ShredRecorderSetting.ReturnOnPause) == !on);
                }
            }),
            ("LN head retains regular colour; the editor clips it at its true position", () =>
            {
                using var visual = new ShredNoteVisual(new ShredHitObject { Fret = 2, Duration = 500 }, new ShredSkinSettings()) { RelativeSizeAxes = Axes.None, Size = new Vector2(40, 26) };
                var head = (ShredGlyph)typeof(ShredNoteVisual).GetField("head", hidden).GetValue(visual);
                visual.Layout(new Vector2(0,100), 1, holding: true, editor: true);
                check(head.Colour.Equals((osu.Framework.Graphics.Colour.ColourInfo)ShredPlayfield.FretColour(2)));
                visual.Layout(new Vector2(0,100), 1, holding: true);
                check(head.Colour.Equals((osu.Framework.Graphics.Colour.ColourInfo)ShredPlayfield.FretColour(2)));
            }),
            ("Moving grid lines render above both judgement orientations", () =>
            {
                using var grid = new ShredEditorPlayfield();
                typeof(ShredEditorPlayfield).GetMethod("load", hidden).Invoke(grid, null);
                var lines = (Box[,])typeof(ShredEditorPlayfield).GetField("gridLines", hidden).GetValue(grid);
                var cursors = (Box[])typeof(ShredEditorPlayfield).GetField("cursors", hidden).GetValue(grid);
                var wham = (Box)typeof(ShredEditorPlayfield).GetField("whamLine", hidden).GetValue(grid);
                check(lines[0,0].Parent == cursors[0].Parent && lines[0,0].Depth < cursors[0].Depth);
                check(lines[2,0].Parent == wham.Parent && lines[2,0].Depth < wham.Depth);
            }),
            ("Undo restores selected off-grid notes", () =>
            {
                var ruleset = new ShredRuleset();
                using var store = new TestRulesetStore(ruleset.RulesetInfo);
                osu.Game.Beatmaps.Formats.Decoder.RegisterDependencies(store);
                var map = new Beatmap<ShredHitObject>(); map.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
                map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                map.HitObjects.AddRange(new[] { new ShredHitObject { StartTime = 1043, Fret = 1 }, new ShredHitObject { StartTime = 2057, Fret = 3 } });
                foreach (var n in map.HitObjects) n.ApplyDefaults(map.ControlPointInfo, map.Difficulty);
                using var beatmap = new EditorBeatmap(map);
                using var handler = new BeatmapEditorChangeHandler(beatmap);
                beatmap.SelectedHitObjects.AddRange(beatmap.HitObjects.Reverse());
                beatmap.RemoveRange(beatmap.SelectedHitObjects.ToArray()); handler.RestoreState(-1);
                equal(1043, beatmap.HitObjects.Min(n => n.StartTime));
                handler.RestoreState(1); check(beatmap.HitObjects.Count == 0);
                handler.RestoreState(-1);
                check(beatmap.SelectedHitObjects.Count == 2);
            }),
            ("Undo recovers an unselected deleted note and returns to its onset", () =>
            {
                var ruleset = new ShredRuleset();
                using var store = new TestRulesetStore(ruleset.RulesetInfo);
                osu.Game.Beatmaps.Formats.Decoder.RegisterDependencies(store);
                var map = new Beatmap<ShredHitObject>(); map.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
                map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                var n = new ShredHitObject { StartTime = 1537, Fret = 1 }; n.ApplyDefaults(map.ControlPointInfo,map.Difficulty); map.HitObjects.Add(n);
                using var beatmap = new EditorBeatmap(map);
                using var handler = new BeatmapEditorChangeHandler(beatmap);
                handler.SaveState(); beatmap.Remove(n); handler.RestoreState(-1); equal(1537,beatmap.HitObjects.Single().StartTime);
            }),
            ("Physical late picks hit through late Great for short notes and LN at all OD values", () =>
            {
                foreach (double od in new[] {0d,2.5,5,7.5,10})
                foreach (int row in new[]{0,1})
                foreach (double duration in new[]{0d,1000})
                foreach (double portion in new[]{0.001,0.25,0.75,1})
                {
                    using var game = new TestSession(); var note = game.Add(1000,row,2,duration:duration);
                    note.HitObject.HitWindows.SetDifficulty(od);
                    foreach (var head in note.HitObject.NestedHitObjects) head.HitWindows.SetDifficulty(od);
                    double late = note.HitObject.HitWindows.WindowFor(osu.Game.Rulesets.Scoring.HitResult.Great) * portion;
                    game.Seek(1000); note.CheckExpiry(); check(!note.HeadJudged);
                    game.Seek(1000+late); note.CheckExpiry(); check(!note.HeadJudged);
                    game.Press(101+row*9); game.Press(105+row*9);
                    check(note.HeadHit);
                }
            }),
            ("Late open notes, wham and hammer-pulls retain a positive hit window", () =>
            {
                using (var game = new TestSession())
                { var note=game.Add(1000,0,0);game.Seek(1050);game.Press(105);check(note.HeadHit); }
                using (var game = new TestSession())
                { var note=game.Add(1000,0,0,ShredPracticeArticulation.Wham);game.Seek(1050);game.Press((int)osu.Game.Rulesets.Shred.ShredAction.Wham);check(note.HeadHit); }
                using (var game = new TestSession())
                { game.Add(1000,0,1);var note=game.Add(2000,0,3,ShredPracticeArticulation.HammerPull);game.Seek(1000);game.Press(100);game.Press(105);game.Seek(2050);game.Press(102);check(note.HeadHit); }
            })
        };
        foreach (var item in cases) { item.run(); Console.WriteLine("PASS "+item.name); }
    }
}
