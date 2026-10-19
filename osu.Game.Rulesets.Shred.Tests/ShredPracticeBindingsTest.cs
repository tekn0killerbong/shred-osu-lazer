using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Input;
using osuTK.Input;

namespace osu.Game.Rulesets.Shred.Tests
{
	[TestFixture]
	public class ShredPracticeBindingsTest
	{
		[TestCase(false)]
		[TestCase(true)]
		public void PresetHasExactlyEighteenUniqueBindings(bool leftHanded)
		{
			ShredPracticeBindings bindings = new ShredPracticeBindings(leftHanded);
			HashSet<Key> keys = new HashSet<Key>();
			for (int row = 0; row < 2; row++)
			{
				for (int action = 0; action < 9; action++)
					Assert.That(keys.Add(bindings.GetKey(row, action)), Is.True);
			}
			Assert.That(keys.Count, Is.EqualTo(18));
		}

		[Test]
		public void DuplicateRebindSwapsAcrossStrings()
		{
			ShredPracticeBindings bindings = new ShredPracticeBindings(false);
			Assert.That(bindings.TryRebind(0, 0, Key.Z), Is.True);
			Assert.That(bindings.GetKey(0, 0), Is.EqualTo(Key.Z));
			Assert.That(bindings.GetKey(1, 0), Is.EqualTo(Key.A));
		}

		[Test]
		public void UnsupportedKeyDoesNotChangeBindings()
		{
			ShredPracticeBindings bindings = new ShredPracticeBindings(false);
			Assert.That(bindings.TryRebind(0, 0, Key.F5), Is.False);
			Assert.That(bindings.GetKey(0, 0), Is.EqualTo(Key.A));
		}

		[Test]
		public void LeftHandedFretRankIncreasesTowardLeft()
		{
			ShredPracticeBindings bindings = new ShredPracticeBindings(true);
			Assert.That(bindings.GetPosition(0, 4).X, Is.LessThan(bindings.GetPosition(0, 0).X));
			Assert.That(bindings.GetKey(0, 0), Is.EqualTo(Key.Semicolon));
			Assert.That(bindings.GetKey(0, 4), Is.EqualTo(Key.H));
		}

		[Test]
		public void LayoutTracksChangedPhysicalKey()
		{
			ShredPracticeBindings bindings = new ShredPracticeBindings(false);
			float oldX = bindings.GetPosition(0, 0).X;
			Assert.That(bindings.TryRebind(0, 0, Key.Q), Is.True);
			Assert.That(bindings.GetPosition(0, 0).X, Is.LessThan(oldX));
			Assert.That(bindings.GetPosition(0, 0).Y, Is.EqualTo(1));
		}

		[Test]
		public void ExactlyTwoStringsAreEnforced()
		{
			Assert.That(ShredPracticeSession.RowCount, Is.EqualTo(2));
			Assert.Throws<ArgumentOutOfRangeException>(() => new ShredPracticeNote(1000, 2, 0));
		}
	}
}
