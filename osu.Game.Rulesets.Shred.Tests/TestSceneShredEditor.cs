using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Tests.Visual;
namespace osu.Game.Rulesets.Shred.Tests
{
	[TestFixture]
	public partial class TestSceneShredEditor : EditorTestScene
	{
		protected override Ruleset CreateEditorRuleset() => new ShredRuleset();
		protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
		{
			Beatmap<ShredHitObject> map = new Beatmap<ShredHitObject>();
			map.BeatmapInfo.Ruleset = ruleset;
			map.Difficulty.CircleSize = 12;
			map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
			map.HitObjects.Add(new ShredHitObject { Row = 0, Fret = 1, StartTime = 1000 });
			return map;
		}
		[Test]
		public void TestComposerLoads()
		{
			AddUntilStep("Shred composer loaded", () => this.ChildrenOfType<ShredHitObjectComposer>().Any(c => c.IsLoaded));
		}
		[Test]
		public void TestPlaceArticulationsAndUndo()
		{
			AddStep("seek to one second", () => EditorClock.Seek(1000));
			place("pick", osuTK.Input.Key.Number2, 2, 1000);
			AddAssert("pick placed", () => EditorBeatmap.HitObjects.OfType<ShredHitObject>().Any(n => n.StartTime == 1000 && n.Fret == 2));
			place("hammer", osuTK.Input.Key.Number3, 3, 1500);
			AddAssert("hammer placed", () => EditorBeatmap.HitObjects.OfType<ShredHitObject>().Any(n => n.StartTime == 1500 && n.Articulation == Gameplay.ShredPracticeArticulation.HammerOn));
			place("open pull", osuTK.Input.Key.Number4, 0, 2000);
			AddAssert("open pull placed", () => EditorBeatmap.HitObjects.OfType<ShredHitObject>().Any(n => n.StartTime == 2000 && n.Fret == 0 && n.Articulation == Gameplay.ShredPracticeArticulation.PullOff));
			AddStep("undo", () => Editor.Undo());
			AddAssert("pull removed", () => EditorBeatmap.HitObjects.All(n => n.StartTime != 2000));
			AddStep("redo", () => Editor.Redo());
			AddAssert("pull restored", () => EditorBeatmap.HitObjects.OfType<ShredHitObject>().Any(n => n.StartTime == 2000 && n.Articulation == Gameplay.ShredPracticeArticulation.PullOff));
		}
		private void place(string name, osuTK.Input.Key key, int column, double time)
		{
			AddStep("select " + name, () => InputManager.Key(key));
			AddStep("position " + name, () =>
			{
				ShredEditorPlayfield grid = this.ChildrenOfType<ShredEditorPlayfield>().Single();
				InputManager.MoveMouseTo(grid.ToScreenSpace(grid.PositionAt(column, time)));
			});
			AddStep("place " + name, () => InputManager.Click(osuTK.Input.MouseButton.Left));
		}

	}
}
