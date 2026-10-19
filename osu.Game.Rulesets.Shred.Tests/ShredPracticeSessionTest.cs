using NUnit.Framework;
using osu.Game.Rulesets.Shred.Gameplay;

namespace osu.Game.Rulesets.Shred.Tests
{
	[TestFixture]
	public class ShredPracticeSessionTest
	{
		[Test]
		public void PullOffCanBeTappedFromOpenWithoutAnotherPick()
		{
			ShredPracticeSession session = new ShredPracticeSession(new[]
			{
				new ShredPracticeNote(1000, 0, 3),
				new ShredPracticeNote(2000, 0, 1, ShredPracticeArticulation.PullOff),
			});
			session.AdvanceTo(1000);
			session.SetFret(0, 2, true);
			session.SetPick(0, 0, true);
			session.AdvanceTo(1500);
			session.SetFret(0, 2, false);
			session.AdvanceTo(2000);
			session.SetFret(0, 0, true);
			Assert.That(session.Hits, Is.EqualTo(2));
			Assert.That(session.Notes[1].Offset, Is.Zero);
		}

		[Test]
		public void PullOffCanBeTappedAboveLowerHeldFret()
		{
			ShredPracticeSession session = new ShredPracticeSession(new[]
			{
				new ShredPracticeNote(1000, 0, 1),
				new ShredPracticeNote(2000, 0, 3, ShredPracticeArticulation.PullOff),
			});
			session.AdvanceTo(1000);
			session.SetFret(0, 0, true);
			session.SetPick(0, 0, true);
			session.AdvanceTo(2000);
			session.SetFret(0, 2, true);
			Assert.That(session.Hits, Is.EqualTo(2));
		}

		[Test]
		public void TappingPullOffUnderHigherHeldFretDoesNotHitUntilRelease()
		{
			ShredPracticeSession session = new ShredPracticeSession(new[]
			{
				new ShredPracticeNote(1000, 0, 3),
				new ShredPracticeNote(2000, 0, 1, ShredPracticeArticulation.PullOff),
			});
			session.AdvanceTo(1000);
			session.SetFret(0, 2, true);
			session.SetPick(0, 0, true);
			session.AdvanceTo(2000);
			session.SetFret(0, 0, true);
			Assert.That(session.Hits, Is.EqualTo(1));
			session.SetFret(0, 2, false);
			Assert.That(session.Hits, Is.EqualTo(2));
		}

		[Test]
        public void TappedPullOffWorksAfterMissWithoutPick()
        {
            var session = new ShredPracticeSession(new[]
            {
                new ShredPracticeNote(1000, 0, 0),
                new ShredPracticeNote(2000, 0, 1, ShredPracticeArticulation.PullOff),
            });
            session.AdvanceTo(2000);
            session.SetFret(0, 0, true);
            Assert.That(session.Hits, Is.EqualTo(1));
            Assert.That(session.Misses, Is.EqualTo(1));
        }

		[Test]
		public void ReleasingOntoHammerOnIsStillRejected()
		{
			ShredPracticeSession session = new ShredPracticeSession(new[]
			{
				new ShredPracticeNote(1000, 0, 3),
				new ShredPracticeNote(2000, 0, 1, ShredPracticeArticulation.HammerOn),
			});
			session.AdvanceTo(1000);
			session.SetFret(0, 0, true);
			session.SetFret(0, 2, true);
			session.SetPick(0, 0, true);
			session.AdvanceTo(2000);
			session.SetFret(0, 2, false);
			Assert.That(session.Hits, Is.EqualTo(1));
		}

		[Test]
		public void NormalNoteNeedsPickAndHighestFret()
		{
			ShredPracticeSession session = new ShredPracticeSession(new[] { new ShredPracticeNote(1000, 0, 3) });
			session.AdvanceTo(1000);
			session.SetFret(0, 0, true);
			session.SetFret(0, 2, true);
			Assert.That(session.Hits, Is.Zero);
			session.SetPick(0, 0, true);
			Assert.That(session.Hits, Is.EqualTo(1));
		}

		[Test]
		public void OpenNoteRejectsHeldFret()
		{
			ShredPracticeSession session = new ShredPracticeSession(new[] { new ShredPracticeNote(1000, 0, 0) });
			session.AdvanceTo(1000);
			session.SetFret(0, 0, true);
			session.SetPick(0, 0, true);
			Assert.That(session.Hits, Is.Zero);
			session.SetFret(0, 0, false);
			Assert.That(session.Hits, Is.Zero);
			session.SetPick(0, 1, true);
			Assert.That(session.Hits, Is.EqualTo(1));
		}

		[Test]
		public void HammerAndPullNeedFretTransitions()
		{
			ShredPracticeSession session = new ShredPracticeSession(new[]
			{
				new ShredPracticeNote(1000, 0, 1),
				new ShredPracticeNote(2000, 0, 3, ShredPracticeArticulation.HammerOn),
				new ShredPracticeNote(3000, 0, 1, ShredPracticeArticulation.PullOff),
				new ShredPracticeNote(4000, 0, 0, ShredPracticeArticulation.PullOff),
			});
			session.AdvanceTo(1000);
			session.SetFret(0, 0, true);
			session.SetPick(0, 0, true);
			session.AdvanceTo(2000);
			session.SetFret(0, 2, true);
			session.AdvanceTo(3000);
			session.SetFret(0, 2, false);
			session.AdvanceTo(4000);
			session.SetFret(0, 0, false);
			Assert.That(session.Hits, Is.EqualTo(4));
			Assert.That(session.Combo, Is.EqualTo(4));
		}

        [TestCase(0)]
        [TestCase(1)]
        public void HammerPullCanStartTheMapAndResumeAfterAnotherMiss(int row)
        {
            var session = new ShredPracticeSession(new[]
            {
                new ShredPracticeNote(1000, row, 3, ShredPracticeArticulation.HammerPull),
                new ShredPracticeNote(2000, row, 2, ShredPracticeArticulation.HammerPull),
                new ShredPracticeNote(3000, row, 1, ShredPracticeArticulation.HammerPull),
            });
            session.AdvanceTo(1000);
            session.SetFret(row, 2, true);
            Assert.That(session.Hits, Is.EqualTo(1));
            session.SetFret(row, 2, false);
            session.AdvanceTo(3000);
            session.SetFret(row, 0, true);
            Assert.That(session.Hits, Is.EqualTo(2));
            Assert.That(session.Misses, Is.EqualTo(1));
        }

		[Test]
		public void HeldPickCannotTriggerAgainButAnotherFingerCan()
		{
			ShredPracticeSession session = new ShredPracticeSession(new[]
			{
				new ShredPracticeNote(1000, 0, 0),
				new ShredPracticeNote(2000, 0, 0),
			});
			session.AdvanceTo(1000);
			session.SetPick(0, 0, true);
			session.AdvanceTo(2000);
			session.SetPick(0, 0, true);
			Assert.That(session.Hits, Is.EqualTo(1));
			session.SetPick(0, 1, true);
			Assert.That(session.Hits, Is.EqualTo(2));
		}

		[TestCase(879, false)]
		[TestCase(880, true)]
		[TestCase(1120, true)]
		[TestCase(1121, false)]
		public void WindowBoundariesAreConsistent(double time, bool expectedHit)
		{
			ShredPracticeSession session = new ShredPracticeSession(new[] { new ShredPracticeNote(1000, 0, 0) });
			session.AdvanceTo(time);
			session.SetPick(0, 0, true);
			Assert.That(session.Hits == 1, Is.EqualTo(expectedHit));
			if (expectedHit)
				Assert.That(session.Notes[0].Offset, Is.EqualTo(time - 1000));
		}

		[Test]
		public void EarlierUnresolvedNoteCannotBeSkipped()
		{
			ShredPracticeSession session = new ShredPracticeSession(new[]
			{
				new ShredPracticeNote(1000, 0, 1),
				new ShredPracticeNote(1050, 0, 2),
			});
			session.AdvanceTo(1025);
			session.SetFret(0, 1, true);
			session.SetPick(0, 0, true);
			Assert.That(session.Hits, Is.Zero);
		}

		[Test]
		public void PausedInputDoesNotJudgeOrTriggerOnResumeWithoutNewEdge()
		{
			ShredPracticeSession session = new ShredPracticeSession(new[] { new ShredPracticeNote(1000, 0, 0) });
			session.AdvanceTo(1000);
			session.SetPick(0, 0, true, false);
			session.SetPick(0, 0, true);
			Assert.That(session.Hits, Is.Zero);
			session.SetPick(0, 0, false);
			session.SetPick(0, 0, true);
			Assert.That(session.Hits, Is.EqualTo(1));
		}
	}
}
