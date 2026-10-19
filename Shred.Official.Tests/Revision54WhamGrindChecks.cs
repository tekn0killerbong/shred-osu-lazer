using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Shred.UI;
using osuTK;

internal static class Revision54WhamGrindChecks
{
	public static void Run()
	{
		int checks = 0;
		void check(bool condition, string label)
		{
			if (!condition) throw new Exception(label);
			checks++;
		}
		foreach (float unit in new[] { 0.7f, 1.3f, 2.6f })
		foreach (float shear in new[] { -1f, -0.466f, 0.466f })
		{
			using ShredWhamGrind glow = new ShredWhamGrind();
			Vector2 centre = new Vector2(700, 360);
			glow.Layout(centre, new Vector2(unit, 24 * unit), unit, shear);
			Drawable[] drawables = glow.Children.ToArray();
			Box[] sparks = glow.Children.OfType<Box>().Where(b => b.Origin == Anchor.CentreLeft).ToArray();
			glow.Animate(0, false, false, true);
			check(drawables.All(d => d.Alpha == 0), "Idle judgement line has no extra glow");
			glow.Animate(20, true, false, true);
			glow.Animate(60, true, false, true);
			check(drawables.Any(d => d.Alpha > 0), "Wham input lights the line");
			check(sparks.All(s => s.Alpha == 0), "An empty press does not emit successful-contact sparks");
			float pressedStrength = drawables.Max(d => d.Alpha);
			for (int time = 80; time <= 320; time += 20) glow.Animate(time, true, true, true);
			check(drawables.Max(d => d.Alpha) > pressedStrength, "A successful sustain intensifies the glow");
			check(sparks.Any(s => s.Alpha > 0), "Held wham emits friction sparks");
			check(glow.Position == centre && glow.Size.Y == 24 * unit, "Glow stays at the judgement geometry");
			check(!glow.HandlePositionalInput && !glow.HandleNonPositionalInput, "Effect never captures input");
			glow.Animate(340, false, false, true);
			check(drawables.Any(d => d.Alpha > 0), "Release has a visible fade rather than an abrupt cut");
			for (int time = 360; time <= 900; time += 20) glow.Animate(time, false, false, true);
			check(drawables.All(d => d.Alpha == 0), "Glow and sparks fully clear after release");
			for (int time = 920; time <= 5000; time += 20) glow.Animate(time, true, true, true);
			check(glow.Children.SequenceEqual(drawables) && drawables.Length < 20, "Long holds reuse the bounded particle pool");
			glow.Animate(5000, true, true, false);
			check(drawables.All(d => d.Alpha == 0), "Pause or a hidden wham lane clears the animation even with a stopped clock");
			glow.Animate(1000, false, false, true);
			check(drawables.All(d => d.Alpha == 0), "Rewinding for retry leaves no stale sparks");
		}
		Console.WriteLine($"PASS v54 wham grind: {checks} animation, geometry, fade, pause, retry and particle-pool checks");
	}
}
