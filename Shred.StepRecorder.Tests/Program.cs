using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Shred.Edit;

internal static class Program
{
    private sealed class Session
    {
        public readonly List<ShredStepRecorderState.Note> Notes = new();
        public readonly List<double> Seeks = new();
        public readonly ShredStepRecorderState Recorder;
        public double Step = 125;
        public Session() => Recorder = new ShredStepRecorderState(0, t => (Math.Floor((t + 0.000001) / Step) + 1) * Step, Notes.Add, _ => { }, Seeks.Add);
    }
    private static void Equal(double expected, double actual)
    {
        if (Math.Abs(expected - actual) > 0.000001) throw new Exception($"Expected {expected}, got {actual}");
    }
    private static void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
    private static void Main()
    {
        var cases = new (string name, Action run)[]
        {
            ("A held fret stays short until an empty step; a pick has no time deadline", () =>
            {
                var s = new Session(); s.Recorder.Press(1); s.Recorder.Pick(new[] { 1 });
                Check(s.Notes[0].Picked && !s.Notes[0].Sustain); Equal(0, s.Notes[0].End);
                s.Recorder.Release(1); Equal(125, s.Recorder.Position); Equal(0, s.Notes[0].End);
            }),
            ("A two-row chord advances exactly once, on its last release", () =>
            {
                var s = new Session(); s.Recorder.Press(1); s.Recorder.Press(8);
                s.Recorder.Release(1); Equal(0, s.Recorder.Position);
                s.Recorder.Release(8); Equal(125, s.Recorder.Position); Equal(1, s.Seeks.Count);
                s.Recorder.Release(8); Equal(1, s.Seeks.Count);
            }),
            ("Empty steps extend held LNs; final release stays at the last tail", () =>
            {
                var s = new Session(); s.Recorder.Press(1); s.Recorder.Press(8); s.Recorder.EmptyStep();
                Check(s.Notes.TrueForAll(n => n.Sustain)); Equal(125, s.Notes[0].End); Equal(125, s.Notes[1].End);
                s.Recorder.Release(1); Equal(125, s.Recorder.Position);
                s.Recorder.EmptyStep(); Equal(125, s.Notes[0].End); Equal(250, s.Notes[1].End);
                s.Recorder.Release(8); Equal(250, s.Recorder.Position); Equal(250, s.Notes[1].End); Equal(2, s.Seeks.Count);
            }),
            ("A short note released beside a held LN neither advances nor extends it", () =>
            {
                var s = new Session(); s.Recorder.Press(1); s.Recorder.EmptyStep();
                s.Recorder.Press(2); s.Recorder.Release(2);
                Equal(125, s.Recorder.Position); Equal(125, s.Notes[0].End);
                s.Recorder.Release(1); Equal(125, s.Recorder.Position); Equal(125, s.Notes[0].End);
            }),
            ("Arrows never create a sustain before an empty step", () =>
            {
                var s = new Session(); s.Recorder.Press(1); s.Recorder.Navigate(250);
                Check(!s.Notes[0].Sustain); Equal(0, s.Notes[0].End);
                s.Recorder.Release(1); Equal(375, s.Recorder.Position);
            }),
            ("After empty step, arrows shorten to the head and extend again", () =>
            {
                var s = new Session(); s.Recorder.Navigate(500); s.Recorder.Press(1); s.Recorder.EmptyStep();
                s.Recorder.Navigate(375); Equal(500, s.Notes[0].End);
                s.Recorder.Navigate(750); Equal(750, s.Notes[0].End);
                s.Recorder.Release(1); Equal(750, s.Recorder.Position); Equal(750, s.Notes[0].End);
            }),
            ("Held LN follows the current 1/12 then 1/4 subdivision without moving its base", () =>
            {
                var s = new Session { Step = 500d / 12 }; s.Recorder.Navigate(500 + 500d / 12); s.Recorder.Press(1);
                s.Recorder.EmptyStep(); Equal(500 + 1000d / 12, s.Notes[0].End);
                s.Step = 125; s.Recorder.EmptyStep(); Equal(625, s.Notes[0].End);
                s.Recorder.Navigate(500); Equal(500 + 500d / 12, s.Notes[0].End);
                s.Recorder.EmptyStep(); Equal(625, s.Notes[0].End);
                Equal(500 + 500d / 12, s.Notes[0].Start);
                s.Recorder.Release(1); Equal(625, s.Recorder.Position);
            }),
            ("OPEN remains short; wham extends only on empty step", () =>
            {
                var s = new Session(); s.Recorder.Pick(new[] { 0 }); s.Recorder.Press(12);
                s.Recorder.EmptyStep(); Check(!s.Notes[0].Sustain && s.Notes[1].Sustain);
                Equal(0, s.Notes[0].End); Equal(125, s.Notes[1].End);
                s.Recorder.Release(12); Equal(125, s.Recorder.Position);
                s.Recorder.Release(0); Equal(125, s.Recorder.Position);
            }),
            ("Released members of an unfinished chord can still be picked", () =>
            {
                var s = new Session(); s.Recorder.Press(1); s.Recorder.Press(2); s.Recorder.Release(1);
                s.Recorder.Pick(new[] { 1, 2 }); Check(s.Notes.TrueForAll(n => n.Picked));
                s.Recorder.Release(2); Equal(125, s.Recorder.Position);
            }),
            ("A held pick waits for release too, regardless of release order", () =>
            {
                foreach (bool pickFirst in new[] { false, true })
                {
                    var s = new Session(); s.Recorder.Press(1); s.Recorder.HoldPickModifier(105); s.Recorder.Pick(new[] { 1 });
                    if (pickFirst) s.Recorder.ReleasePickModifier(105); else s.Recorder.Release(1);
                    Equal(0, s.Recorder.Position);
                    if (pickFirst) s.Recorder.Release(1); else s.Recorder.ReleasePickModifier(105);
                    Equal(125, s.Recorder.Position); Check(s.Notes[0].Picked);
                }
            }),
			("A single LN release does not seek, and the next short gesture advances", () =>
			{
				var s = new Session(); s.Recorder.Press(1); s.Recorder.EmptyStep();
				s.Recorder.Release(1); s.Recorder.Release(1);
				Equal(125, s.Recorder.Position); Equal(1, s.Seeks.Count); Equal(125, s.Notes[0].End);
				s.Recorder.Press(1); s.Recorder.Release(1);
				Equal(125, s.Notes[1].Start); Check(!s.Notes[1].Sustain); Equal(250, s.Recorder.Position);
			}),
			("A short note at the LN end cannot add a release step in either order", () =>
			{
				foreach (bool longFirst in new[] { false, true })
				{
					var s = new Session(); s.Recorder.Press(1); s.Recorder.EmptyStep(); s.Recorder.Press(8);
					if (longFirst) { s.Recorder.Release(1); s.Recorder.Release(8); }
					else { s.Recorder.Release(8); s.Recorder.Release(1); }
					Equal(125, s.Recorder.Position); Equal(1, s.Seeks.Count);
					Equal(125, s.Notes[0].End); Equal(125, s.Notes[1].Start); Check(!s.Notes[1].Sustain);
				}
			}),
			("Empty steps without held notes preserve later short-note stepping", () =>
			{
				var s = new Session(); s.Recorder.EmptyStep(); s.Recorder.EmptyStep();
				s.Recorder.Press(1); s.Recorder.Release(1);
				Equal(250, s.Notes[0].Start); Equal(375, s.Recorder.Position); Check(!s.Notes[0].Sustain);
			}),
			("A legacy pick modifier cannot add a release step after an LN", () =>
			{
				foreach (bool pickFirst in new[] { false, true })
				{
					var s = new Session(); s.Recorder.HoldPickModifier(105); s.Recorder.Press(1); s.Recorder.EmptyStep();
					if (pickFirst) { s.Recorder.ReleasePickModifier(105); s.Recorder.Release(1); }
					else { s.Recorder.Release(1); s.Recorder.ReleasePickModifier(105); }
					Equal(125, s.Recorder.Position); Equal(1, s.Seeks.Count);
				}
			}),
            ("Finish cancels held input without inventing an extra step", () =>
            {
                var s = new Session(); s.Recorder.Press(1); s.Recorder.EmptyStep(); s.Recorder.Finish();
                s.Recorder.Release(1); Equal(125, s.Recorder.Position); Check(!s.Recorder.HasHeldInputs);
				s.Recorder.Press(2); s.Recorder.Release(2); Equal(250, s.Recorder.Position);
            })
        };
        foreach (var item in cases) { item.run(); Console.WriteLine("PASS " + item.name); }
    }
}
