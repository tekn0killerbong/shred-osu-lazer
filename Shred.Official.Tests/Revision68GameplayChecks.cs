using osu.Game.Rulesets.Scoring;

internal static partial class GameplayChecks
{
	public static void RunRevision68()
	{
		int count = 0;
		foreach (double rate in new[] { .25, .5, 1, 2 })
		foreach (int row in new[] { 0, 1 })
		foreach (double duration in new[] { 0d, 1000d })
		{
			using var game = new TestSession(rate: rate);
			int key = 100 + row * 9;
			var first = new[] { game.Add(1000, row, 1, duration: duration), game.Add(1000, row, 2, duration: duration) };
			double stop = 2000;
			var next = new[] { game.Add(stop + 160, row, 3), game.Add(stop + 160, row, 4) };
			game.Seek(900); game.Press(key); game.Press(key + 1);
			game.Seek(1000); game.Press(key + 5); game.Press(key + 6);
			if (!first.All(n => n.HeadHit)) throw new Exception("v68 initial chord failed");
			game.Seek(stop); foreach (var n in first) n.CheckExpiry();
			game.Release(key + 6); game.FlushPicks();
			if (next.Any(n => n.HeadJudged)) throw new Exception($"v68 stopping a chord/LN spuriously judges a future chord: rate={rate}, row={row}, duration={duration}, results={string.Join(',', next.Select(n => n.Result.Type))}");
			game.Release(key + 5); game.Release(key); game.Release(key + 1);
			game.Seek(stop + 160); game.Press(key + 2); game.Press(key + 3); game.Press(key + 5); game.Press(key + 6);
			if (!next.All(n => n.HeadHit)) throw new Exception("v68 future chord must remain playable normally");
			count += 3;
		}
		Console.WriteLine($"PASS v68: {count} release and future-note checks");
	}
}
