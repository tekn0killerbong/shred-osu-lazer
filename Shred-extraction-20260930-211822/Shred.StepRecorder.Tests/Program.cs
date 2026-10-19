using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Shred.Edit;

internal static class Program
{
	private sealed class Session
	{
		public readonly List<ShredStepRecorderState.Note> Notes = new List<ShredStepRecorderState.Note>();
		public readonly ShredStepRecorderState Recorder;
		public Session() => Recorder = new ShredStepRecorderState(0, t => t + 125, Notes.Add, n => { }, t => { });
	}
	private static void equal(double expected, double actual)
	{
		if (Math.Abs(expected - actual) > 0.000001) throw new Exception($"Expected {expected}, got {actual}");
	}
	private static void check(bool value) { if (!value) throw new Exception("Assertion failed"); }
	private static void Main()
	{
		var cases = new (string name, Action run)[]
		{
			("Fret creates immediately; pick upgrades it", () =>
			{
				var s = new Session(); s.Recorder.Press(1, 0);
				equal(1, s.Notes.Count); check(!s.Notes[0].Picked);
				s.Recorder.Pick(new[] { 1 }, 100); check(s.Notes[0].Picked);
				s.Recorder.Release(1, 150); equal(125, s.Recorder.Position);
			}),
            ("Threshold is one second and does not move the grid", () =>
            {
                var s = new Session(); s.Recorder.Press(1, 0);
                s.Recorder.Tick(999); check(!s.Notes[0].Sustain);
                s.Recorder.Tick(1000); check(s.Notes[0].Sustain); equal(0, s.Recorder.Position);
                s.Recorder.EmptyStep(1100); equal(125, s.Notes[0].End); equal(125, s.Recorder.Position);
                s.Recorder.Release(1, 1101); equal(125, s.Notes[0].End);
            }),
            ("Short chord advances on its last release without any previous step", () =>
            {
                var s = new Session(); s.Recorder.Press(1, 0); s.Recorder.Press(2, 30);
                s.Recorder.Release(1, 100); equal(0, s.Recorder.Position);
                s.Recorder.Release(2, 150); equal(125, s.Recorder.Position);
                equal(0, s.Notes[0].End); equal(0, s.Notes[1].End);
            }),
            ("Armed LN extends on a short release at the same step", () =>
            {
                var s = new Session(); s.Recorder.Press(1, 0); s.Recorder.Tick(1000);
                s.Recorder.Press(2, 1100); s.Recorder.Press(3, 1120);
                s.Recorder.Release(2, 1200); equal(0, s.Recorder.Position);
                s.Recorder.Release(3, 1250); equal(125, s.Recorder.Position); equal(125, s.Notes[0].End);
                s.Recorder.Release(1, 1251); equal(125, s.Notes[0].End);
            }),
            ("Later short press does not postpone another hold threshold", () =>
            {
                var s = new Session(); s.Recorder.Press(1, 0); s.Recorder.Press(2, 900);
                s.Recorder.Release(2, 1100); check(s.Notes[0].Sustain); equal(125, s.Notes[0].End);
                s.Recorder.Pick(new[] { 1 }, 1101); check(!s.Notes[0].Picked);
            }),
            ("First short release from a navigated position advances", () =>
            {
                var r = new ShredStepRecorderState(5000, t => t + 125, n => { }, n => { }, t => { });
                r.Press(1, 100); r.Release(1, 200); equal(5125, r.Position);
            }),
            ("LN tail and new short head may coincide", () =>
            {
                var s = new Session(); s.Recorder.Press(1, 0); s.Recorder.Tick(1000); s.Recorder.EmptyStep(1100);
                s.Recorder.Release(1, 1200); s.Recorder.Press(1, 1300); s.Recorder.Release(1, 1400);
                equal(s.Notes[0].End, s.Notes[1].Start); equal(250, s.Recorder.Position);
            }),
			("Arrow navigation resizes held LN without adding a release step", () =>
			{
				var s = new Session(); s.Recorder.Press(1, 0); s.Recorder.Tick(1000);
				s.Recorder.Navigate(250, 1100); equal(250, s.Notes[0].End);
				s.Recorder.Navigate(125, 1200); equal(125, s.Notes[0].End); check(s.Recorder.IsHeld(1));
				s.Recorder.Release(1, 1300); equal(125, s.Recorder.Position);
				s.Recorder.Navigate(500, 1400); equal(125, s.Notes[0].End);
			}),
			("Backwards navigation clamps at heads and can extend again", () =>
			{
				var s = new Session(); s.Recorder.Navigate(500, 0); s.Recorder.Press(1, 10); s.Recorder.Press(2, 20);
				s.Recorder.Navigate(625, 30); equal(625, s.Notes[0].End); equal(625, s.Notes[1].End);
				s.Recorder.Navigate(375, 40); equal(500, s.Notes[0].End); equal(500, s.Notes[1].End);
				s.Recorder.Navigate(750, 50); equal(750, s.Notes[0].End); equal(750, s.Notes[1].End);
				s.Recorder.Release(1, 60); s.Recorder.Release(2, 70); equal(750, s.Recorder.Position);
			}),
			("Navigation resizes wham but never turns OPEN into a sustain", () =>
			{
				var s = new Session(); s.Recorder.Press(12, 0); s.Recorder.Press(0, 10);
				s.Recorder.Navigate(250, 20); equal(250, s.Notes[0].End); equal(0, s.Notes[1].End);
				s.Recorder.Navigate(125, 30); equal(125, s.Notes[0].End); check(!s.Notes[1].Sustain);
			}),
            ("Open remains short and wham extends with an empty step", () =>
            {
                var s = new Session(); s.Recorder.Pick(new[] { 0 }, 0); s.Recorder.Release(0, 100);
                equal(0, s.Notes[0].End); equal(125, s.Recorder.Position);
                s.Recorder.Press(12, 200, true); s.Recorder.Tick(1200); equal(125, s.Recorder.Position);
                s.Recorder.EmptyStep(1300); equal(250, s.Notes[1].End);
            })
		};
		foreach (var item in cases) { item.run(); Console.WriteLine("PASS " + item.name); }
	}
}
