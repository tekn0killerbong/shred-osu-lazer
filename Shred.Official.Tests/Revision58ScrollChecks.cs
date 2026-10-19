using osu.Framework.Graphics;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.UI;
using osuTK;

internal static partial class GameplayChecks
{
	public static void RunRevision58Scroll()
	{
		int checks = 0;
		void Check(bool ok, string label)
		{
			if (!ok) throw new Exception(label);
			checks++;
		}
		foreach (double rate in new[] { 0.01, 0.25, 0.5, 0.75, 1, 1.37, 1.5, 2, 4 })
		foreach (bool hero in new[] { false, true })
		foreach (Vector2 size in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080) })
		{
			using TestSession baseline = new TestSession(hero);
			using TestSession adjusted = new TestSession(hero, rate);
			foreach (TestSession game in new[] { baseline, adjusted })
			{
				game.Field.RelativeSizeAxes = Axes.None;
				game.Field.Size = size;
				game.Seek(1000);
			}
			foreach (int column in Enumerable.Range(0, 13))
			{
				ShredHitObject note = new ShredHitObject { Row = column / 6 % 2, Fret = column % 6 };
				if (column == 12) note.Articulation = ShredPracticeArticulation.Wham;
				double realLead = ShredPlayfield.ApproachTime * 0.8;
				Vector2 expected = baseline.Field.PositionAt(note, 1000 + realLead);
				Vector2 actual = adjusted.Field.PositionAt(note, 1000 + realLead * rate);
				Check((actual - expected).Length < 0.01, $"Real-time position changed: {column}, {rate}, OG={hero}, {size}");
				baseline.Seek(1050);
				adjusted.Seek(1000 + 50 * rate);
				Vector2 referenceMovement = baseline.Field.PositionAt(note, 1000 + realLead) - expected;
				Vector2 movement = adjusted.Field.PositionAt(note, 1000 + realLead * rate) - actual;
				Check((movement - referenceMovement).Length < 0.01, "50 ms of real time must move the note by the same pixels");
				baseline.Seek(1000); adjusted.Seek(1000);
				Vector2 expectedTail = baseline.Field.PositionAt(note, 1000 + realLead + 200);
				Vector2 tail = adjusted.Field.PositionAt(note, 1000 + (realLead + 200) * rate);
				Check((tail - expectedTail).Length < 0.01, "LN tails must use the same real-time projection as their heads");
			}
			Check(adjusted.Field.MaximumVisibleLeadTime >= 11485 * rate + 500, "Lifetime must include the slowest scroll at the selected rate");
			Check(adjusted.Field.MaximumWhamVisibleLeadTime >= 11485 * rate + 500, "Wham lifetime must cover accelerated playback");
		}
		foreach (double invalid in new[] { 0d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
			Check(ShredScrollSpeed.ValidPlaybackRate(invalid) == 1, "Invalid clock rates must not break geometry");
		Check(ShredScrollSpeed.ValidPlaybackRate(-1.5) == 1.5, "Rewinding must retain the positive scale of the projection");
		Console.WriteLine($"PASS v58 {checks} absolute-scroll geometry, real-time velocity, LN, wham, lifetime and rate-fallback checks");
	}
}
