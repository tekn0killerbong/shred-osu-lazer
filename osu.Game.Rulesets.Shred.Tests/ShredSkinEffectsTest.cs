using System;
using System.IO;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Shred.UI;
using osuTK;
using osuTK.Graphics;
using NUnit.Framework;
using osu.Game.Rulesets.Shred.Skinning;
using osu.Game.Rulesets.Shred.Scoring;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Beatmaps;
namespace osu.Game.Rulesets.Shred.Tests
{
	[TestFixture]
	public class ShredSkinEffectsTest
	{
		[Test]
		public void TrigonometricAnglesBecomeScreenVectorsAndMatchingShear()
		{
			ShredSkinSettings settings = new ShredSkinSettings();
			Assert.That(settings.Direction(0).X, Is.GreaterThan(0));
			Assert.That(settings.Direction(0).Y, Is.GreaterThan(0));
			Assert.That(settings.Direction(1).X, Is.LessThan(0));
			Assert.That(settings.Direction(1).Y, Is.LessThan(0));
			for (int row = 0; row < 2; row++)
				Assert.That(settings.Direction(row).X + settings.Shear(row) * settings.Direction(row).Y, Is.EqualTo(0).Within(0.00001));
		}
		[Test]
		public void PerColumnWidthsAndSpacingDefineCentresAndOpenSpan()
		{
			using StringReader reader = new StringReader("[Shred]\nColumnWidth:40,50,60,70,80\nColumnSpacing:10,20,30,40\nHaloColour:255,200,60\n");
			ShredSkinSettings settings = ShredSkinSettings.Read(reader);
			Assert.That(settings.FretCentre(2) - settings.FretCentre(1), Is.EqualTo(55));
			Assert.That(settings.OpenWidth, Is.EqualTo(400));
			Assert.That(settings.HaloColour.G, Is.EqualTo(200f / 255).Within(0.001));
		}
		[Test]
		public void ScalarColumnSettingsExpandToBothRowsGeometry()
		{
			using StringReader reader = new StringReader("[Shred]\nColumnWidth:52\nColumnSpacing:12\nScrollAngleTop:-90\nScrollAngleBottom:90\n");
			ShredSkinSettings settings = ShredSkinSettings.Read(reader);
			Assert.That(settings.ColumnWidth, Is.EqualTo(new float[] {52,52,52,52,52}));
			Assert.That(settings.Shear(0), Is.EqualTo(0).Within(0.00001));
		}
		[TestCase("ColumnWidth:1,2")]
		[TestCase("ColumnSpacing:-1")]
		[TestCase("ScrollAngleTop:0")]
		[TestCase("HaloOpacity:NaN")]
		[TestCase("AnimationFPS:0")]
		public void InvalidSkinValuesAreRejected(string value)
		{
			using StringReader reader = new StringReader("[Shred]\n" + value);
			Assert.Throws<FormatException>(() => ShredSkinSettings.Read(reader));
		}
		[TestCase(-250.01, false)]
		[TestCase(-250, true)]
		[TestCase(0, true)]
		[TestCase(250, true)]
		[TestCase(250.01, false)]
		public void WrongPickWindowDoesNotPunishArbitraryPreparation(double offset, bool expected)
		{
			ShredHitWindows windows = new ShredHitWindows();
			windows.SetDifficulty(5);
			Assert.That(ShredInputJudgement.IsWrongPickInWindow(windows, offset), Is.EqualTo(expected));
		}
		[TestCase(-65, 34)]
		[TestCase(-90, 26)]
		[TestCase(-120, 50)]
		public void LowerGlyphTopLeftAlignsBelowUpperCentre(float angle, float gap)
		{
			ShredSkinSettings settings = new ShredSkinSettings { TopAngle = angle, RowGap = gap };
			using ShredGlyph upper = new ShredGlyph(Color4.White, shear: settings.Shear(0))
			{
				Position = new Vector2(200, 200), Size = new Vector2(48, 26), Origin = Anchor.Centre
			};
			using ShredGlyph lower = new ShredGlyph(Color4.White, shear: settings.Shear(1))
			{
				Position = upper.Position + new Vector2(settings.EffectiveRowOffsetBottom, gap), Size = upper.Size, Origin = Anchor.Centre
			};
            Assert.That(lower.ScreenSpaceDrawQuad.TopLeft.X,
                Is.EqualTo(upper.ScreenSpaceDrawQuad.Centre.X).Within(0.002));
		}
		[Test]
		public void ManualRowLayoutRemainsAvailable()
		{
			using StringReader reader = new StringReader("[Shred]\nAlignRows:false\nRowOffsetBottom:-22\nScrollAngleBottom:80\n");
			ShredSkinSettings settings = ShredSkinSettings.Read(reader);
			Assert.That(settings.EffectiveRowOffsetBottom, Is.EqualTo(-22));
			Assert.That(settings.Shear(0), Is.Not.EqualTo(settings.Shear(1)));
		}
		[Test]
		public void NativeMissBreaksNativeCombo()
		{
			ShredRuleset ruleset = new ShredRuleset();
			Beatmap<ShredHitObject> beatmap = new Beatmap<ShredHitObject>();
			ShredHitObject first = new ShredHitObject { StartTime = 1000, Row = 0, Fret = 1 };
			ShredHitObject second = new ShredHitObject { StartTime = 2000, Row = 0, Fret = 2 };
			beatmap.HitObjects.Add(first);
			beatmap.HitObjects.Add(second);
			ScoreProcessor processor = new ScoreProcessor(ruleset);
			processor.ApplyBeatmap(beatmap);
			processor.ApplyResult(new JudgementResult(first, first.CreateJudgement()) { Type = HitResult.Perfect });
			Assert.That(processor.Combo.Value, Is.EqualTo(1));
			processor.ApplyResult(new JudgementResult(second, second.CreateJudgement()) { Type = HitResult.Miss });
			Assert.That(processor.Combo.Value, Is.Zero);
		}
	}
}
