using NUnit.Framework;
using osu.Game.Rulesets.Shred.Input;

namespace osu.Game.Rulesets.Shred.Tests
{
	[TestFixture]
	public class ShredStringStateTest
	{
		[Test]
		public void HighestFretWinsAndReleaseRevealsLowerFret()
		{
			ShredStringState state = new ShredStringState();
			Assert.That(state.SelectedFret, Is.EqualTo(0));
			state.SetFret(1, true);
			state.SetFret(4, true);
			state.SetFret(0, true);
			Assert.That(state.SelectedFret, Is.EqualTo(5));
			state.SetFret(4, false);
			Assert.That(state.SelectedFret, Is.EqualTo(2));
		}

		[Test]
		public void PicksAreIndependentAndIgnoreRepeat()
		{
			ShredStringState state = new ShredStringState();
			Assert.That(state.SetPick(0, true), Is.True);
			Assert.That(state.SetPick(0, true), Is.False);
			Assert.That(state.SetPick(1, true), Is.True);
			Assert.That(state.IsPickHeld(0), Is.True);
			Assert.That(state.SetPick(0, false), Is.False);
			Assert.That(state.SetPick(0, true), Is.True);
		}

		[Test]
		public void FretsAndPicksStayOnTheirOwnString()
		{
			ShredStringState first = new ShredStringState();
			ShredStringState second = new ShredStringState();
			first.SetFret(3, true);
			first.SetPick(2, true);
			Assert.That(second.SelectedFret, Is.EqualTo(0));
			Assert.That(second.IsPickHeld(2), Is.False);
		}

		[Test]
		public void ResetReturnsToOpenAndRearmsPicks()
		{
			ShredStringState state = new ShredStringState();
			state.SetFret(4, true);
			state.SetPick(3, true);
			state.Reset();
			Assert.That(state.SelectedFret, Is.EqualTo(0));
			Assert.That(state.IsPickHeld(3), Is.False);
			Assert.That(state.SetPick(3, true), Is.True);
		}
	}
}
