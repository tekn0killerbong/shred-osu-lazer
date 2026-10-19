using System.Text;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Database;
using Realms;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Compatibility;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;

internal static class EditorCompatibilityChecks
{
    public static void Run(Beatmap imported)
    {
        var ruleset = new ShredRuleset();
        Assert(ruleset is not ILegacyRuleset && ruleset.RulesetInfo.OnlineID == -1, "Discovery remains a custom ruleset");
        Assert(ruleset.RulesetInfo.CreateInstance() is ILegacyRuleset, "Instantiated Shred supports official editor save gate");
        Assert(typeof(ShredRuleset).Assembly.GetTypes().Count(t => t.IsPublic && t.IsSubclassOf(typeof(Ruleset))) == 1, "Exactly one discoverable ruleset, no Mania bundled");
        var playable = new FlatWorkingBeatmap(imported).GetPlayableBeatmap(ruleset.RulesetInfo);
        Assert(playable.BeatmapInfo.Ruleset.ShortName == "shred", "Playable identity selects the Shred editor processor without test-side relabelling");
        using var editor = new EditorBeatmap(playable);
        using var history = new BeatmapEditorChangeHandler(editor);
        Assert(editor.BeatmapInfo.Ruleset.OnlineID == 3 && ruleset.RulesetInfo.OnlineID == -1,
            "Editor identity is detached from installed identity");
        using var selectionHistory = new ShredEditorHistory(editor, history);
        editor.SelectedHitObjects.Add(editor.HitObjects.First());
        string initial = Fingerprint(editor);
        _ = history.CurrentStateHash;
        var note = (ShredHitObject)editor.HitObjects.First();
        editor.BeginChange();
        note.Fret = 5; note.Row = 0; note.Duration = 812.375;
        note.Articulation = ShredPracticeArticulation.HammerPull;
        editor.Update(note);
        editor.EndChange();
        string changed = Fingerprint(editor);
        Assert(changed != initial, "Editor mutation updates native data");
        history.RestoreState(-1);
        Assert(Fingerprint(editor) == initial, "Official Ctrl+Z restores rows, frets, articulation and duration");
        Assert(editor.SelectedHitObjects.Count == 1 && editor.HitObjects.Contains(editor.SelectedHitObjects[0]),
            "Shred selection restores through official undo");
        history.RestoreState(1);
        Assert(Fingerprint(editor) == changed, "Official Ctrl+Y restores edited data");
        editor.BeginChange();
        var added = new ShredHitObject { StartTime = 4321.125, Duration = 123.625, Row = 1, Fret = 0, Articulation = ShredPracticeArticulation.Wham,
            Samples = new List<HitSampleInfo> { new ConvertHitObjectParser.FileHitSampleInfo("custom-hit.wav", 63), new HitSampleInfo(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT, volume: 63) } };
        editor.Add(added);
        editor.EndChange();
        string withAdded = Fingerprint(editor);
        history.RestoreState(-1);
        Assert(Fingerprint(editor) == changed, "Official undo removes newly inserted wham");
        history.RestoreState(1);
        Assert(Fingerprint(editor) == withAdded, "Official redo recreates wham");
        string encoded = Encode(editor);
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(encoded));
        using var reader = new LineBufferedReader(bytes);
        var decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
        var reopened = new FlatWorkingBeatmap(decoded).GetPlayableBeatmap(ruleset.RulesetInfo);
        Assert(Fingerprint(reopened) == withAdded, "Official save/decode/reopen preserves all note data");
        var custom = reopened.HitObjects.Cast<ShredHitObject>().Single(n => n.StartTime == added.StartTime);
        Assert(custom.Samples.OfType<ConvertHitObjectParser.FileHitSampleInfo>().Single().Filename == "custom-hit.wav"
            && custom.Samples.Count == 2 && custom.Samples[0].Volume == 63, "Custom hitsound and additions survive marker roundtrip");
        Assert(reopened.HitObjects.All(h => h.Samples.OfType<ConvertHitObjectParser.FileHitSampleInfo>().All(s => !s.Filename.StartsWith(ShredEditorCodec.Prefix))), "Metadata samples never reach gameplay");
        CheckRealmWriteback(editor);
        editor.BeginChange();
        foreach(var obj in editor.HitObjects.ToArray()) editor.Remove(obj);
        editor.EndChange();
        _ = Encode(editor);
        history.RestoreState(-1);
        Assert(Fingerprint(editor) == withAdded, "Delete-all and undo restore native objects");
    }


    public static void RunMatrix()
    {
        var map = new Beatmap<ShredHitObject>();
        map.BeatmapInfo.Ruleset = new ShredRuleset().RulesetInfo;
        foreach (int row in new[] { 0, 1 })
        foreach (int fret in Enumerable.Range(0, 6))
        foreach (var articulation in Enum.GetValues<ShredPracticeArticulation>())
        foreach (double duration in new[] { 0d, 0.25, 1234.567 })
            map.HitObjects.Add(new ShredHitObject { Row = row, Fret = fret, Articulation = articulation,
                StartTime = 2000 + map.HitObjects.Count * 250.125, Duration = duration,
                Samples = new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM, volume: 42) } });
        using var editor = new EditorBeatmap(map);
        using var history = new BeatmapEditorChangeHandler(editor);
        string before = Fingerprint(editor);
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(Encode(editor)));
        using var reader = new LineBufferedReader(bytes);
        var decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
        var restored = new FlatWorkingBeatmap(decoded).GetPlayableBeatmap(new ShredRuleset().RulesetInfo);
        Assert(Fingerprint(restored) == before, $"All {map.HitObjects.Count} row/fret/articulation/duration combinations roundtrip");
        var empty = new Beatmap<ShredHitObject>();
        empty.BeatmapInfo.Ruleset = new ShredRuleset().RulesetInfo;
        using var emptyEditor = new EditorBeatmap(empty);
        using var emptyHistory = new BeatmapEditorChangeHandler(emptyEditor);
        _ = emptyHistory.CurrentStateHash;
        emptyEditor.Add(new ShredHitObject { StartTime = 1000, Fret = 1 });
        emptyHistory.RestoreState(-1);
        Assert(emptyEditor.HitObjects.Count == 0, "New empty Shred difficulty supports first insertion and undo");
        emptyHistory.RestoreState(1);
        Assert(emptyEditor.HitObjects.Single() is ShredHitObject, "New difficulty redo restores native type");
    }

    private static void CheckRealmWriteback(EditorBeatmap editor)
    {
        string folder = Path.Combine(Path.GetTempPath(), "shred-editor-save-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        var config = new RealmConfiguration(Path.Combine(folder, "save.realm"));
        try
        {
            using var realm = Realm.GetInstance(config);
            var native = new ShredRuleset().RulesetInfo;
            var liveInfo = new BeatmapInfo(native) { ID = editor.BeatmapInfo.ID };
            var set = new BeatmapSetInfo { Beatmaps = { liveInfo } };
            liveInfo.BeatmapSet = set;
            realm.Write(() => { realm.Add(native); realm.Add(set); });
            var detached = set.Detach();
            detached.Beatmaps[0].Ruleset = editor.BeatmapInfo.Ruleset.Clone();
            detached.Beatmaps[0].DifficultyName = "saved from Shred editor";
            realm.Write(() => detached.CopyChangesToRealm(set));
            Assert(liveInfo.Ruleset.ShortName == "shred" && liveInfo.Ruleset.OnlineID == -1 && native.OnlineID == -1
                && liveInfo.DifficultyName == "saved from Shred editor", "Official save writeback retains custom Realm identity");
        }
        finally { Realm.DeleteRealm(config); Directory.Delete(folder, true); }
    }
    private static string Encode(IBeatmap map)
    {
        using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        new LegacyBeatmapEncoder(map, null, null).Encode(writer);
        return writer.ToString();
    }
    private static string Fingerprint(IBeatmap map) => string.Join(";", map.HitObjects.Cast<ShredHitObject>()
        .Select(n => FormattableString.Invariant($"{n.StartTime:R}/{n.Duration:R}/{n.Row}/{n.Fret}/{n.Articulation}")));
    private static void Assert(bool value, string text)
    {
        if (!value) throw new Exception(text);
        Console.WriteLine("PASS " + text);
    }
}
