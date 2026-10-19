using System;
using System.Linq;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Scoring;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.GameplayTest;

internal static partial class Program
{
    private static void RunRevision23Tests()
    {
        var cases = new (string name, Action run)[]
        {
            ("Recorder off leaves both keyboard and UI pauses at their current position", () =>
            {
                foreach (bool ui in new[] { false, true })
                {
                    using var session = new PlaybackSession();
                    session.Playback.RecorderEnabled.Value = false;
                    session.At(1000); session.Playback.Toggle(true); session.Playback.Tick();
                    session.At(2000);
                    if (ui) session.Clock.Stop(); else session.Playback.Toggle(true);
                    session.Playback.Tick();
                    check(!session.Clock.IsRunning); equal(2000, session.Clock.CurrentTime);
                }
            }),
            ("Recorder Space returns but Ctrl Space stays put even after subsequent updates", () =>
            {
                foreach (bool ctrl in new[] { false, true })
                {
                    using var session = new PlaybackSession();
                    session.At(1000); session.Playback.Toggle(true); session.Playback.Tick();
                    session.At(2000); session.Playback.Toggle(!ctrl);
                    session.Playback.Tick(); session.Playback.Tick();
                    double expected = 1000;
                    if (ctrl) expected = 2000;
                    check(!session.Clock.IsRunning); equal(expected, session.Clock.CurrentTime);
                }
            }),
            ("Turning recorder off during playback prevents UI return without losing its preference", () =>
            {
                using var session = new PlaybackSession();
                session.At(1000); session.Playback.Toggle(true); session.Playback.Tick();
                session.At(2000); session.Playback.RecorderEnabled.Value = false;
                session.Clock.Stop(); session.Playback.Tick();
                equal(2000, session.Clock.CurrentTime); check(session.Playback.ReturnOnPause.Value);
            }),
            ("Recorder UI pause returns while navigation or the unchecked preference suppresses return", () =>
            {
                for (int mode = 0; mode < 3; mode++)
                {
                    using var session = new PlaybackSession();
                    session.At(1000); session.Playback.Toggle(true); session.Playback.Tick();
                    session.At(2000);
                    if (mode == 1) session.Playback.NotifyNavigation();
                    if (mode == 2) session.Playback.ReturnOnPause.Value = false;
                    session.Clock.Stop(); session.Playback.Tick();
                    double expected = 2000;
                    if (mode == 0) expected = 1000;
                    equal(expected, session.Clock.CurrentTime);
                }
            }),
            ("Short notes LN heads and wham expire immediately after late Great at every OD", () =>
            {
                foreach (double od in new[] { 0d, 5, 10 })
                foreach (int kind in new[] { 0, 1, 2 })
                {
                    using var game = new TestSession();
                    var type = ShredPracticeArticulation.Pick;
                    double duration = 0;
                    if (kind > 0) duration = 2000;
                    if (kind == 2) type = ShredPracticeArticulation.Wham;
                    var note = game.Add(1000, 0, 1, type, duration);
                    note.HitObject.HitWindows.SetDifficulty(od);
                    foreach (var nested in note.HitObject.NestedHitObjects) nested.HitWindows.SetDifficulty(od);
                    double window = note.HitObject.HitWindows.WindowFor(HitResult.Great);
                    game.Seek(1000 + window); note.CheckExpiry(); check(!note.HeadJudged);
                    game.Seek(1000 + window + 0.01); note.CheckExpiry(); check(note.HeadJudged && !note.HeadHit);
                }
            }),
            ("A missed note no longer consumes the following rapid stream's physical picks", () =>
            {
                using var game = new TestSession();
                var notes = Enumerable.Range(0, 30).Select(i => game.Add(1000 + 50 * i, 0, 1)).ToArray();
                game.Seek(900); game.Press(100);
                for (int i = 1; i < notes.Length; i++)
                {
                    game.Seek(1061 + i * 50); game.Press(105); game.Release(105);
                    check(notes[i].HeadHit);
                }
                check(notes[0].Result.Type == HitResult.Miss);
            }),
            ("A physical pick cannot resurrect its armed note beyond late Great", () =>
            {
                using var game = new TestSession();
                var note = game.Add(1000, 0, 2);
                game.Seek(1000); game.Press(105);
                game.Seek(1100.01); game.Press(101);
                check(note.HeadJudged && !note.HeadHit);
            }),
            ("Valid buffered hammer-picks survive automatic expiry until their captured result commits", () =>
            {
                foreach (bool sustain in new[] { false, true })
                {
                    using var game = new TestSession();
                    game.Add(1000, 0, 1); game.Add(1500, 0, 1);
                    double duration = 0;
                    if (sustain) duration = 1000;
                    var note = game.Add(2000, 0, 1, ShredPracticeArticulation.Pick, duration);
                    game.Seek(1000); game.Press(100); game.Press(105);
                    game.Seek(1500); game.Press(106);
                    game.Seek(2095); game.Release(106);
                    game.Seek(2101); note.CheckExpiry(); check(!note.HeadJudged);
                    game.FlushPicks(); check(note.HeadHit);
                }
            }),
            ("Editor Test completion cannot exit before the last LN tail and a full outro", () =>
            {
                var completion = new EditorTestCompletion();
                check(!completion.Update(true, false, 1000, 5000, 1000, 1000));
                check(!completion.Update(true, false, 4000, 5000, 4000, 1000));
                check(!completion.Update(true, false, 5000, 5000, 5000, 1000));
                check(!completion.Update(true, false, 5999, 5000, 5999, 1000));
                check(completion.Update(true, false, 6000, 5000, 6000, 1000));
            }),
            ("Existing non-pooled notes before the test cursor cannot emit duplicate misses", () =>
            {
                using var game = new TestSession();
                var previous = game.Add(1000, 0, 1);
                var atCursor = game.Add(2000, 0, 1);
                var later = game.Add(3000, 0, 1);
                var mark = typeof(EditorPlayer).GetMethod("preventMissOnDrawable",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                int results = 0;
                previous.OnNewResult += (_, _) => results++;
                foreach (var note in new[] { previous, atCursor, later })
                    mark.Invoke(null, new object[] { note, 2000d });
                game.Seek(2000); previous.CheckExpiry();
                check(previous.HeadHit && !atCursor.HeadJudged && !later.HeadJudged);
                equal(0, results);
            }),
            ("Editor Test outro is cancelled by pause rewind or retracted score completion", () =>
            {
                for (int mode = 0; mode < 3; mode++)
                {
                    var completion = new EditorTestCompletion();
                    check(!completion.Update(true, false, 5000, 5000, 0, 1000));
                    double position = 5100;
                    if (mode == 2) position = 4900;
                    check(!completion.Update(mode != 0, mode == 1, position, 5000, 500, 1000));
                    check(!completion.Update(true, false, 5200, 5000, 1500, 1000));
                    check(completion.Update(true, false, 6200, 5000, 2500, 1000));
                }
            })
        };
        foreach (var item in cases) { item.run(); Console.WriteLine("PASS " + item.name); }
    }

    private partial class PlaybackProbe : ShredEditorPlayback
    {
        public void Tick() => base.Update();
    }

    private sealed class PlaybackSession : IDisposable
    {
        public readonly EditorClock Clock = new EditorClock();
        public readonly PlaybackProbe Playback = new PlaybackProbe();
        private readonly FramedBeatmapClock underlying = new FramedBeatmapClock(false, false, new AdjustableTestClock());

        public PlaybackSession()
        {
            typeof(EditorClock).GetField("underlyingClock", hidden).SetValue(Clock, underlying);
            typeof(ShredEditorPlayback).GetProperty("clock", hidden).SetValue(Playback, Clock);
            Playback.RecorderEnabled.Value = true;
        }
        public void At(double time) => underlying.Seek(time);
        public void Dispose() { Playback.Dispose(); underlying.Dispose(); Clock.Dispose(); }
    }

    private sealed class AdjustableTestClock : IAdjustableClock
    {
        public double CurrentTime { get; private set; }
        public double Rate { get; set; } = 1;
        public bool IsRunning { get; private set; }
        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void Reset() { CurrentTime = 0; IsRunning = false; }
        public void ResetSpeedAdjustments() => Rate = 1;
        public bool Seek(double time) { CurrentTime = time; return true; }
    }
}
