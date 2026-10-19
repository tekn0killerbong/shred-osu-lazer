using osu.Framework.Graphics;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.UI;
using osuTK;

internal static partial class GameplayChecks
{
	public static void RunRevision54Whams()
	{
		int checks = 0;
		void Check(bool ok, string label)
		{
			if (!ok) throw new Exception(label);
			checks++;
		}
		foreach (Vector2 size in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(1280, 960), new Vector2(2560, 1080) })
		foreach (float angle in new[] { -45f, -65f, -80f })
		foreach (bool hero in new[] { false, true })
		{
			using TestSession game = new TestSession(hero);
			ShredPlayfield field = game.Field;
			field.RelativeSizeAxes = Axes.None;
			field.Size = size;
			field.Skin.TopAngle = angle;
			game.Seek(0);
			ShredHitObject regular = new ShredHitObject { Row = 0, Fret = 3 };
			ShredHitObject wham = new ShredHitObject { Articulation = ShredPracticeArticulation.Wham };
			float fretMovement = (field.PositionAt(regular, 1100) - field.PositionAt(regular, 1000)).Length;
			float whamMovement = (field.PositionAt(wham, 1100) - field.PositionAt(wham, 1000)).Length;
			Check(Math.Abs(fretMovement - whamMovement) < 0.001, $"Wham velocity differs from regular lane at {size}, angle {angle}, OG {hero}");
			Check((field.PositionAt(wham, 0) - field.WhamReceiver).Length < 0.001, "Wham reaches judgement at its exact time");
			double entryLead = Math.Max(0, size.X - field.WhamReceiver.X) / field.WhamTravel * 11485;
			Check(field.MaximumWhamVisibleLeadTime > entryLead, "Slowest-speed lifetime must start before wham enters screen");
			if (!hero) Check(field.MaximumVisibleLeadTime == 11485 * ShredScrollSpeed.MaximumPlaybackRate + 500, "Regular-note lifetime must not increase along with wham lifetime");
			Check(field.WhamSize.Y > 10 * field.Unit && field.WhamSize.Y < field.NoteSize(3).Y, "Wham thickness is between former bar and regular notes");
			if (!hero)
			{
				float shear = field.Skin.Shear(0);
				Vector2 centre = field.WhamLineCentre;
				float half = ShredWhamGeometry.LineHeight(field.Skin) * field.Unit / 2;
				Vector2 a = centre + new Vector2(-field.WhamLineWidth / 2 + shear * half, -half);
				Vector2 b = centre + new Vector2(-field.WhamLineWidth / 2 - shear * half, half);
				float minDistance = float.MaxValue;
				for (int row = 0; row < 2; row++)
				{
					Vector2 fret = field.Receiver(row, 5);
					Vector2 noteSize = field.NoteSize(5);
					Vector2 p = fret + new Vector2(noteSize.X / 2 + shear * noteSize.Y / 2, -noteSize.Y / 2);
					Vector2 q = fret + new Vector2(noteSize.X / 2 - shear * noteSize.Y / 2, noteSize.Y / 2);
					minDistance = Math.Min(minDistance, Math.Min(Math.Min(distance(a, p, q), distance(b, p, q)), Math.Min(distance(p, a, b), distance(q, a, b))));
				}
				Check(Math.Abs(minDistance - (field.Skin.RowGap - field.Skin.NoteHeight) * field.Unit) < 0.001, "Actual slanted-edge gap must equal row gap");
			}
		}
		foreach (float shear in new[] { -1f, -0.466f, 0.466f, 1f })
		foreach (float y in new[] { -6f, 0f, 6f })
		{
			Vector2 start = new Vector2(100, 100), end = new Vector2(300, 100);
			Vector2 edge = start + new Vector2(-shear * y, y);
			Check(ShredWhamGeometry.ContainsVisibleBar(edge + new Vector2(2, 0), start, end, 16, shear, 1), "Slanted bar interior is clickable");
			Check(!ShredWhamGeometry.ContainsVisibleBar(edge - new Vector2(2, 0), start, end, 16, shear, 1), "Empty side of slanted bar is not clickable");
		}
		using (TestSession game = new TestSession())
		{
			TestNote note = game.Add(1000, 0, 0, ShredPracticeArticulation.Wham, 1000);
			game.Seek(1000); game.Press((int)ShredAction.Wham);
			Check(note.HeadHit, "Wham attack still judges at timestamp");
			game.Seek(2000); note.CheckExpiry();
			Check(note.Judged && note.Result.IsHit, "Wham held through its tail still succeeds");
		}
		Console.WriteLine($"PASS v54 whams: {checks} speed, lifetime, geometry, hit-test and judgement checks");
	}
	private static float distance(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 direction = b - a;
		float t = Math.Clamp(Vector2.Dot(p - a, direction) / direction.LengthSquared, 0, 1);
		return (p - (a + direction * t)).Length;
	}
}
