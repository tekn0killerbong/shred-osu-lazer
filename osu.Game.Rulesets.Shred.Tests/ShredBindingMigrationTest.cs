using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Input.Bindings;
using osu.Game.Rulesets.Shred.UI;
using osuTK.Graphics;
namespace osu.Game.Rulesets.Shred.Tests
{
	[TestFixture]
	public class ShredBindingMigrationTest
	{
		[Test]
		public void TemplateBindingsCannotOverrideASOrConflictWithZX()
		{
			IKeyBinding[] legacy =
			{
				new KeyBinding(InputKey.Z, (ShredAction)0),
				new KeyBinding(InputKey.X, (ShredAction)1),
				new KeyBinding(InputKey.Z, (ShredAction)9),
				new KeyBinding(InputKey.X, (ShredAction)10),
			};
			var resolved = ShredBindingProfile.Resolve(legacy);
			Assert.That(resolved.Count, Is.EqualTo(18));
			foreach (var expected in new ShredRuleset().GetDefaultKeyBindings())
			{
				var actual = resolved.Single(b => Convert.ToInt32(b.Action) == Convert.ToInt32(expected.Action));
				Assert.That(actual.KeyCombination.ToString(), Is.EqualTo(expected.KeyCombination.ToString()));
			}
		}
		[Test]
		public void CurrentCustomBindingIsPreservedAndMissingActionsAreFilled()
		{
			IKeyBinding custom = new KeyBinding(InputKey.Q, ShredAction.Row1Fret1);
			var resolved = ShredBindingProfile.Resolve(new[] { custom });
			Assert.That(resolved.Count, Is.EqualTo(18));
			Assert.That(resolved.Single(b => Convert.ToInt32(b.Action) == 100), Is.SameAs(custom));
		}
		[Test]
		public void AllStableActionsDecodeToTwoRowsAndNineSlots()
		{
			foreach (ShredAction value in Enum.GetValues<ShredAction>())
			{
				Assert.That(ShredBindingProfile.TryDecode(value, out int row, out int action), Is.True);
				Assert.That(row, Is.InRange(0, 1));
				Assert.That(action, Is.InRange(0, 8));
			}
			Assert.That(ShredBindingProfile.TryDecode((ShredAction)0, out _, out _), Is.False);
			Assert.That(ShredBindingProfile.TryDecode((ShredAction)17, out _, out _), Is.False);
		}
		[Test]
		public void ParallelogramShearMatchesMirroredLaneSlope()
		{
			using ShredGlyph glyph = new ShredGlyph(Color4.White);
			Assert.That(glyph.Shear.X, Is.EqualTo(-70f / 150f));
			Assert.That(glyph.Shear.Y, Is.Zero);
		}
	}
}
