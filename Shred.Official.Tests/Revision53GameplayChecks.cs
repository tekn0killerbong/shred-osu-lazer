using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred.Gameplay;

internal static partial class GameplayChecks
{
	public static void RunRevision53()
	{
		int cases = 0;
		foreach (int row in new[] { 0, 1 })
		foreach (int totalFrets in new[] { 3, 4 })
		foreach (int firstSize in Enumerable.Range(1, totalFrets - 1))
		foreach (bool mirror in new[] { false, true })
		foreach (bool reverseRelease in new[] { false, true })
		foreach (bool firstGroupMoves in new[] { false, true })
		foreach (double offset in new[] { -75d, 0, 75d })
		{
			using var game = new TestSession();
			int key = 100 + row * 9;
			int[] firstFrets = Enumerable.Range(1, firstSize).ToArray();
			int[] secondFrets = Enumerable.Range(firstSize + 1, totalFrets - firstSize).ToArray();
			if (mirror)
			{
				firstFrets = firstFrets.Select(f => 6 - f).ToArray();
				secondFrets = secondFrets.Select(f => 6 - f).ToArray();
			}
			int[] firstPicks = Enumerable.Range(key + 5, firstSize).ToArray();
			int[] secondPicks = Enumerable.Range(key + 5 + firstSize, totalFrets - firstSize).ToArray();
			var first = firstFrets.Select(f => game.Add(500, row, f)).ToArray();
			var second = secondFrets.Select(f => game.Add(1000, row, f)).ToArray();
			int[] movingFrets = secondFrets, anchorFrets = firstFrets;
			int[] movingPicks = secondPicks;
			if (firstGroupMoves)
			{
				movingFrets = firstFrets;
				anchorFrets = secondFrets;
				movingPicks = firstPicks;
			}
			var repeats = new List<TestNote[]>();
			for (int cycle = 0; cycle < 3; cycle++)
			{
				if (firstGroupMoves)
				{
					repeats.Add(movingFrets.Select(f => game.Add(1500 + cycle * 1000, row, f)).ToArray());
					repeats.Add(anchorFrets.Select(f => game.Add(2000 + cycle * 1000, row, f)).ToArray());
				}
				else
				{
					repeats.Add(anchorFrets.Select(f => game.Add(1500 + cycle * 1000, row, f)).ToArray());
					repeats.Add(movingFrets.Select(f => game.Add(2000 + cycle * 1000, row, f)).ToArray());
				}
			}
			game.Seek(400);
			foreach (int f in firstFrets) game.Press(key + f - 1);
			game.Seek(500); foreach (int p in firstPicks) game.Press(p);
			if (first.Any(n => !n.HeadHit)) throw new Exception("First chord setup failed");
			game.Seek(900); foreach (int f in secondFrets) game.Press(key + f - 1);
			game.Seek(1000); foreach (int p in secondPicks) game.Press(p);
			if (second.Any(n => !n.HeadHit)) throw new Exception("Second chord setup failed");
			int[] releaseOrder = movingPicks;
			if (reverseRelease) releaseOrder = movingPicks.Reverse().ToArray();
			if (firstGroupMoves)
			{
				game.Seek(1200);
				foreach (int p in releaseOrder) game.Release(p);
			}
			for (int step = 0; step < repeats.Count; step++)
			{
				game.Seek(1500 + step * 500 + offset);
				bool press = step % 2 == 1;
				if (firstGroupMoves) press = !press;
				if (press)
					foreach (int p in movingPicks) game.Press(p);
				else
				{
					foreach (int p in releaseOrder) game.Release(p);
					if (repeats[step].Any(n => n.HeadHit)) throw new Exception("Chord return bypassed the pick buffer");
					game.FlushPicks();
				}
				if (repeats[step].Any(n => !n.HeadHit))
					throw new Exception($"Prepared alternation blocked: row={row}, split={firstSize}+{totalFrets-firstSize}, mirror={mirror}, firstMoves={firstGroupMoves}, offset={offset}, step={step}");
			}
			cases++;
		}
		Console.WriteLine($"PASS v53 {cases} prepared 2+1 / 1+2 / 3+1 / 1+3 / 2+2 alternations: both rows, mirrored frets, both anchors and release orders, three cycles, -75/0/+75 ms");
		RunFiveFretStrums();
		RunStrumGuards();
		RunStrumCapacityGuards();
	}

	private static void RunFiveFretStrums()
	{
		int cases = 0;
		foreach (int row in new[] { 0, 1 })
		foreach (int firstSize in new[] { 1, 2, 3, 4 })
		foreach (bool mirror in new[] { false, true })
		foreach (bool reverseRelease in new[] { false, true })
		foreach (bool firstGroupMoves in new[] { false, true })
		foreach (double offset in new[] { -75d, 0, 75d })
		{
			using var game = new TestSession();
			int key = 100 + row * 9;
			int[] firstFrets = Enumerable.Range(1, firstSize).ToArray();
			int[] secondFrets = Enumerable.Range(firstSize + 1, 5 - firstSize).ToArray();
			if (mirror)
			{
				firstFrets = firstFrets.Select(f => 6 - f).ToArray();
				secondFrets = secondFrets.Select(f => 6 - f).ToArray();
			}
			var first = firstFrets.Select(f => game.Add(500, row, f)).ToArray();
			var second = secondFrets.Select(f => game.Add(1000, row, f)).ToArray();
			TestNote reanchor = null;
			int[] firstPicks = Enumerable.Range(key + 5, firstSize - 1).ToArray();
			int[] secondPicks = Enumerable.Range(key + 5 + firstSize - 1, 5 - firstSize).ToArray();
			if (firstSize == 1)
			{
				// The four-note physical preparation temporarily uses all picks.
				// Re-establish the single's pick while the other three remain held.
				reanchor = game.Add(1400, row, firstFrets[0]);
				firstPicks = new[] { key + 5 };
				secondPicks = new[] { key + 6, key + 7, key + 8 };
			}
			int[] movingFrets = secondFrets, anchorFrets = firstFrets, movingPicks = secondPicks;
			if (firstGroupMoves)
			{
				movingFrets = firstFrets; anchorFrets = secondFrets; movingPicks = firstPicks;
			}
			var repeats = new List<TestNote[]>();
			for (int cycle = 0; cycle < 3; cycle++)
			{
				repeats.Add(movingFrets.Select(f => game.Add(2000 + cycle * 1000, row, f)).ToArray());
				repeats.Add(anchorFrets.Select(f => game.Add(2500 + cycle * 1000, row, f)).ToArray());
			}
			game.Seek(400); foreach (int f in firstFrets) game.Press(key + f - 1);
			game.Seek(500); for (int p = 0; p < firstSize; p++) game.Press(key + 5 + p);
			if (first.Any(n => !n.HeadHit)) throw new Exception("Five-fret first setup failed");
			game.Seek(900); foreach (int f in secondFrets) game.Press(key + f - 1);
			game.Seek(1000);
			for (int p = firstSize; p < 4; p++) game.Press(key + 5 + p);
			game.Release(key + 5 + firstSize - 1);
			game.Press(key + 5 + firstSize - 1);
			if (second.Any(n => !n.HeadHit)) throw new Exception("Five-fret second setup failed");
			if (reanchor != null)
			{
				game.Seek(1400); game.Release(key + 5); game.Press(key + 5);
				if (!reanchor.HeadHit) throw new Exception("Prepared single could not reclaim its pick after the four-note chord");
			}
			int[] releaseOrder = movingPicks;
			if (reverseRelease) releaseOrder = movingPicks.Reverse().ToArray();
			game.Seek(1500); foreach (int p in releaseOrder) game.Release(p);
			for (int step = 0; step < repeats.Count; step++)
			{
				game.Seek(2000 + step * 500 + offset);
				if (step % 2 == 0)
				{
					for (int p = 0; p < movingPicks.Length; p++)
					{
						game.Press(movingPicks[p]);
						if (p < movingPicks.Length - 1 && repeats[step].All(n => n.HeadHit))
							throw new Exception("Discounted chord completed before all required moving picks arrived");
					}
				}
				else
				{
					foreach (int p in releaseOrder) game.Release(p);
					if (repeats[step].Any(n => n.HeadHit)) throw new Exception("Five-fret return bypassed buffer");
					game.FlushPicks();
				}
				if (repeats[step].Any(n => !n.HeadHit))
					throw new Exception($"Five-fret alternation blocked: row={row}, split={firstSize}+{5-firstSize}, mirror={mirror}, firstMoves={firstGroupMoves}, offset={offset}, step={step}");
			}
			cases++;
		}
		Console.WriteLine($"PASS v53 {cases} prepared 4+1 / 1+4 / 3+2 / 2+3 alternations with four picks, both rows/directions/anchors, three cycles and timing offsets");
	}

	private static void RunStrumGuards()
	{
		int cases = 0;
		foreach (int row in new[] { 0, 1 })
		foreach (string change in new[] { "missing", "repressed", "partner-repressed", "extra", "hp", "pause", "rewind", "all-picks-up", "miss", "none" })
		{
			using var game = new TestSession();
			int key = 100 + row * 9;
			var first = new[] { game.Add(500, row, 1), game.Add(500, row, 2) };
			var second = new[] { game.Add(1000, row, 3), game.Add(1000, row, 4) };
			ShredPracticeArticulation type = ShredPracticeArticulation.Pick;
			if (change == "hp") type = ShredPracticeArticulation.HammerPull;
			var target = new[] { game.Add(1750, row, 1, type), game.Add(1750, row, 2, type) };
			TestNote missed = null;
			if (change == "miss") missed = game.Add(1250, row, 5);
			game.Seek(400); game.Press(key); game.Press(key + 1);
			game.Seek(500); game.Press(key + 5); game.Press(key + 6);
			game.Seek(900); game.Press(key + 2); game.Press(key + 3);
			game.Seek(1000); game.Press(key + 7); game.Press(key + 8);
			if (first.Concat(second).Any(n => !n.HeadHit)) throw new Exception("Guard setup failed");
			game.Seek(1200);
			switch (change)
			{
				case "missing": game.Release(key); break;
				case "repressed": game.Release(key); game.Press(key); break;
				case "partner-repressed": game.Release(key + 3); game.Press(key + 3); break;
				case "extra": game.Press(key + 4); break;
				case "pause": game.Ruleset.IsPaused.Value = true; game.Field.ProcessPendingPicks(); game.Ruleset.IsPaused.Value = false; break;
				case "rewind": game.Seek(200); game.Field.ProcessPendingPicks(); break;
				case "all-picks-up": for (int p = 0; p < 4; p++) game.Release(key + 5 + p); break;
			}
			game.Seek(1750); missed?.CheckExpiry();
			if (change == "all-picks-up") { game.Press(key + 5); game.Press(key + 6); }
			else { game.Release(key + 7); game.Release(key + 8); game.FlushPicks(); }
			bool shouldHit = change == "miss" || change == "none";
			if (shouldHit && target.Any(n => !n.HeadHit)) throw new Exception("Valid prepared return blocked by " + change);
			if (!shouldHit && target.Any(n => n.HeadHit)) throw new Exception("Strum exception leaked through " + change);
			if (missed != null && missed.Result.Type != HitResult.Miss) throw new Exception("Miss fixture did not miss");
			cases++;
		}
		Console.WriteLine($"PASS v53 {cases} strum guards: missing/repressed frets, changed partner, unrelated fret, hammer-pulls, pause/rewind, release all picks, and recovery after miss");
	}

	private static void RunStrumCapacityGuards()
	{
		int cases = 0;
		foreach (int row in new[] { 0, 1 })
		foreach (string mode in new[] { "anchor-too-small", "moving-too-small", "both-discounted", "early", "late", "different-shape", "open", "no-spill" })
		{
			using var game = new TestSession();
			int key = 100 + row * 9;
			for (int f = 1; f <= 3; f++) game.Add(500, row, f);
			game.Add(1000, row, 4); game.Add(1000, row, 5);
			int[] wanted = new[] { 1, 2, 3 };
			if (mode == "both-discounted") wanted = new[] { 4, 5 };
			if (mode == "different-shape") wanted = new[] { 1, 2 };
			if (mode == "open") wanted = new[] { 0 };
			var target = wanted.Select(f => game.Add(1750, row, f)).ToArray();
			double laterTime = 2150;
			if (mode == "no-spill") laterTime = 1800;
			var later = new[] { game.Add(laterTime, row, 1), game.Add(laterTime, row, 2), game.Add(laterTime, row, 3) };
			game.Seek(400); for (int f = 0; f < 3; f++) game.Press(key + f);
			game.Seek(500); for (int p = 0; p < 3; p++) game.Press(key + 5 + p);
			game.Seek(900); game.Release(key + 7); game.Press(key + 3); game.Press(key + 4);
			game.Seek(1000); game.Press(key + 7); game.Press(key + 8);
			game.Seek(1200);
			if (mode == "anchor-too-small") game.Release(key + 6);
			if (mode == "moving-too-small") { game.Release(key + 5); game.Release(key + 6); }
			if (mode == "both-discounted") { game.Release(key + 7); game.Release(key + 8); }
			double time = 1750;
			if (mode == "early") time -= 150;
			if (mode == "late") time += 150;
			game.Seek(time);
			if (mode == "moving-too-small") game.Press(key + 5);
			else if (mode == "both-discounted") game.Press(key + 7);
			else { game.Release(key + 7); game.Release(key + 8); game.FlushPicks(); }
			int expected = 0;
			// v60: insufficient capacity leaves the entire chord unjudged.
            // Partial head successes are no longer allowed.
			if (mode == "no-spill") expected = 3;
			if (target.Count(n => n.HeadHit) != expected) throw new Exception($"Invalid strum capacity: {mode}, row={row}, hits={target.Count(n => n.HeadHit)} expected={expected}");
			if (later.Any(n => n.HeadHit)) throw new Exception("Strum capacity spilled into a later chord");
			cases++;
		}
		foreach (int row in new[] { 0, 1 })
		{
			using var game = new TestSession();
			int key = 100 + row * 9;
			game.Add(500, row, 1); game.Add(1000, row, 2); var target = game.Add(1500, row, 1);
			game.Seek(400); game.Press(key); game.Seek(500); game.Press(key + 5);
			game.Seek(900); game.Press(key + 1); game.Seek(1000); game.Press(key + 6);
			game.Seek(1500); game.Release(key + 6); game.FlushPicks();
			if (target.HeadHit) throw new Exception("Ordinary 1+1 fret priority was bypassed");
			cases++;
		}
		Console.WriteLine($"PASS v53 {cases} capacity guards: insufficient picks, no double discount, early/late windows, exact prepared shapes, OPEN, next chord and ordinary 1+1 priority");
	}
}
