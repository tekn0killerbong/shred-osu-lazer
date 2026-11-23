using osu.Game.Rulesets.Shred.Gameplay;

internal static partial class GameplayChecks
{
	public static void RunRevision69()
	{
		int checks = 0;
		void check(bool condition, string message)
		{
			if (!condition) throw new Exception("v69: " + message);
			checks++;
		}

		// An anchor may predate every judgement window. All other picks must
		// still be fresh inputs for this chord, with the normal row allocation.
		foreach (int upper in Enumerable.Range(0, 5))
		foreach (int lower in Enumerable.Range(0, 5))
		{
			if (upper + lower < 2) continue;
			int[] picks = Enumerable.Range(105, upper).Concat(Enumerable.Range(114, lower)).ToArray();
			int[] frets = Enumerable.Range(100, upper).Concat(Enumerable.Range(109, lower)).ToArray();
			foreach (int anchor in picks)
			foreach (bool reverse in new[] { false, true })
			foreach (double rate in new[] { .5, 1, 2 })
			{
				using var game = new TestSession(rate: rate);
				TestNote[] chord(double time) => Enumerable.Range(1, upper).Select(f => game.Add(time, 0, f))
					.Concat(Enumerable.Range(1, lower).Select(f => game.Add(time, 1, f))).ToArray();
				var groups = new[] { 1000, 1400, 1800, 2200 }.Select(t => chord(t)).ToArray();
				int[] mobile = picks.Where(p => p != anchor).ToArray();
				if (reverse) Array.Reverse(mobile);
				string label = $"{upper}+{lower}, anchor={anchor}, reverse={reverse}, rate={rate}";
				game.Seek(500); game.Press(anchor);
				game.Seek(900); foreach (int fret in frets) game.Press(fret);
				game.Seek(1000);
				for (int i = 0; i < mobile.Length; i++)
				{
					game.Press(mobile[i]);
					if (i < mobile.Length - 1) check(groups[0].All(n => !n.HeadJudged), "startup stays atomic: " + label);
				}
				check(groups[0].All(n => n.HeadHit), "preheld anchor starts full shred: " + label);
				game.Seek(1400); foreach (int pick in mobile) game.Release(pick);
				check(groups[1].All(n => !n.HeadJudged), "return uses existing buffer: " + label);
				game.FlushPicks(); check(groups[1].All(n => n.HeadHit), "first return: " + label);
				game.Seek(1800); foreach (int pick in mobile) game.Press(pick);
				check(groups[2].All(n => n.HeadHit), "following attack: " + label);
				game.Seek(2200); foreach (int pick in mobile) game.Release(pick); game.Release(anchor);
				game.FlushPicks(); check(groups[3].All(n => !n.HeadJudged), "stopping cancels return: " + label);
				foreach (int pick in picks.Reverse()) game.Press(pick);
				check(groups[3].All(n => n.HeadHit), "normal full chord remains playable: " + label);
			}
		}

		foreach (int row in new[] { 0, 1 })
		foreach (bool wrongFrets in new[] { false, true })
		foreach (double duration in new[] { 0d, 200d })
		{
			using var game = new TestSession(); int key = 100 + row * 9;
			var missed = Enumerable.Range(1, 3).Select(f => game.Add(1000, row, f)).ToArray();
			var restart = Enumerable.Range(1, 3).Select(f => game.Add(1400, row, f, duration: duration)).ToArray();
			var returned = Enumerable.Range(1, 3).Select(f => game.Add(1800, row, f)).ToArray();
			game.Seek(500); game.Press(key + 5); game.Press(key); game.Press(key + 1);
			if (!wrongFrets) game.Press(key + 2);
			game.Seek(1000); game.Press(key + 6);
			if (wrongFrets) game.Press(key + 7);
			game.Seek(1165); missed[0].CheckExpiry();
			check(missed.All(n => n.HeadJudged && !n.HeadHit), "initial attempt really misses");
			game.Release(key + 6); if (wrongFrets) game.Release(key + 7);
			game.Seek(1300); if (wrongFrets) game.Press(key + 2);
			game.Seek(1400); game.Press(key + 6); game.Press(key + 7);
			check(restart.All(n => n.HeadHit), "resume after an initial miss without re-pressing anchor");
			game.Seek(1600); foreach (var note in restart) note.CheckExpiry();
			game.Seek(1800); game.Release(key + 6); game.Release(key + 7); game.FlushPicks();
			check(returned.All(n => n.HeadHit), "return after recovered short/LN chord");
		}

		foreach (bool anchorFirst in new[] { false, true })
		foreach (double rate in new[] { .5, 1, 2 })
		{
			using var game = new TestSession(rate: rate);
			var first = Enumerable.Range(1, 3).Select(f => game.Add(1000, 0, f)).ToArray();
			var next = Enumerable.Range(1, 3).Select(f => game.Add(1040, 0, f)).ToArray();
			if (anchorFirst) { game.Seek(500); game.Press(105); }
			game.Seek(900); game.Press(100); game.Press(101); game.Press(102);
			game.Seek(1000); game.Press(106); game.Press(107);
			if (!anchorFirst)
			{
				check(first.All(n => !n.HeadJudged), "mobile-first ordinary chord waits for last pick");
				game.Press(105);
			}
			check(first.All(n => n.HeadHit), "both pick orders hit the same chord");
			game.Seek(1005); game.Press(108);
			check(next.All(n => !n.HeadJudged), "excess press cannot hit next dense chord");
			game.Release(108);
			game.Seek(1040); game.Release(106); game.Release(107); game.FlushPicks();
			check(next.All(n => n.HeadHit), "next dense return remains playable");
		}

		foreach (bool late in new[] { false, true })
		{
			using var game = new TestSession();
			var chord = Enumerable.Range(1, 3).Select(f => game.Add(1000, 0, f)).ToArray();
			game.Seek(500); game.Press(105);
			game.Seek(1000); game.Press(106); game.Press(107);
			check(chord.All(n => !n.HeadJudged), "anchor cannot bypass missing frets");
			double fretTime = 1020;
			if (late) fretTime = 1080;
			game.Seek(fretTime); game.Press(100); game.Press(101); game.Press(102);
			check(chord.All(n => n.HeadHit == !late), "late fret correction keeps Great boundary");
		}

		using (var game = new TestSession())
		{
			var chord = Enumerable.Range(1, 3).Select(f => game.Add(1000, 0, f)).ToArray();
			game.Seek(500); game.Press(105); game.Press(106);
			game.Seek(1000); game.Press(100); game.Press(101); game.Press(102); game.Press(107);
			check(chord.All(n => !n.HeadJudged), "two old anchors cannot replace fresh mobile picks");
		}
		using (var game = new TestSession())
		{
			var chord = new[] { game.Add(1000, 0, 1), game.Add(1000, 0, 2) };
			game.Seek(500); game.Press(114);
			game.Seek(1000); game.Press(100); game.Press(101); game.Press(105);
			check(chord.All(n => !n.HeadJudged), "startup preserves ordinary row allocation");
		}
		foreach (int row in new[] { 0, 1 })
		{
			using var game = new TestSession(); int key = 100 + row * 9;
			var single = game.Add(500, row, 1);
			var chord = Enumerable.Range(1, 3).Select(f => game.Add(1000, row, f)).ToArray();
			game.Seek(500); game.Press(key); game.Press(key + 5);
			check(single.HeadHit, "anchor can come from a previously played note");
			game.Seek(900); game.Press(key + 1); game.Press(key + 2);
			game.Seek(1000); game.Press(key + 6); game.Press(key + 7);
			check(chord.All(n => n.HeadHit), "previous single's anchor starts an overlapping full chord");
		}
		foreach (int anchor in new[] { 105, 106, 114, 115, 116 })
		{
			using var game = new TestSession(hero: true);
			var chord = Enumerable.Range(1, 5).Select(f => game.Add(1000, 1, f)).ToArray();
			var returned = Enumerable.Range(1, 5).Select(f => game.Add(1400, 1, f)).ToArray();
			int[] mobile = new[] { 105, 106, 114, 115, 116 }.Where(p => p != anchor).ToArray();
			game.Seek(500); game.Press(anchor);
			game.Seek(900); foreach (int fret in Enumerable.Range(109, 5)) game.Press(fret);
			game.Seek(1000); foreach (int pick in mobile) game.Press(pick);
			check(chord.All(n => n.HeadHit), "OG pooled picks can start a five-note chord");
			game.Seek(1400); foreach (int pick in mobile) game.Release(pick); game.FlushPicks();
			check(returned.All(n => n.HeadHit), "OG anchored return stays playable");
		}
		Console.WriteLine($"PASS v69: {checks} anchored startup, recovery and double-hit checks");
	}
}
