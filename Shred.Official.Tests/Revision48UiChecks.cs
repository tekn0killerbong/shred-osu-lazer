using System.Diagnostics;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.StateChanges;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Graphics.Containers;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components.TernaryButtons;
using osu.Game.Skinning;
using osuTK;
using osuTK.Input;

internal static class Revision48UiChecks
{
	private const BindingFlags private_fields = BindingFlags.Instance | BindingFlags.NonPublic;

	private static void apply(IInput change)
		=> change.Apply(EditorUiProbe.PointerInput.CurrentState, EditorUiProbe.PointerInput);

	// Exercise hit-testing, masking, hover and input propagation, not TriggerClick.
	private static IEnumerable<Action> click(Drawable button)
	{
		yield return () =>
		{
			for (var parent = button.Parent; parent != null; parent = parent.Parent)
				if (parent is OsuScrollContainer scroll)
				{
					scroll.ScrollIntoView(button, false);
					break;
				}
		};
		yield return () => apply(new MousePositionAbsoluteInput { Position = button.ScreenSpaceDrawQuad.Centre });
		yield return () =>
		{
			if (!button.IsHovered) throw new Exception("Pointer target is not visible/hovered: " + button.GetType().Name);
			apply(new MouseButtonInput(MouseButton.Left, true));
		};
		yield return () =>
		{
			var watch = Stopwatch.StartNew();
			apply(new MouseButtonInput(MouseButton.Left, false));
			Console.WriteLine($"POINTER click {watch.Elapsed.TotalMilliseconds:F2} ms");
		};
	}

	private static void checkLoadedSounds(DrawableShredEditorNote note, string bank, int count)
	{
		var sounds = note.ChildrenOfType<PoolableSkinnableSample>().ToArray();
		if (sounds.Length != count) throw new Exception("Unexpected number of loaded samples: " + sounds.Length);
		foreach (var sound in sounds)
		{
			var info = (HitSampleInfo)typeof(PoolableSkinnableSample).GetField("sampleInfo", private_fields)!.GetValue(sound)!;
			var sample = (ISample)sound.Sample.GetType().GetField("sample", private_fields)!.GetValue(sound.Sample)!;
			string name = bank + "-" + info.Name;
			double expected = Revision45Checks.Files.Single(f => f.name == name).ms;
			if (sample.Name != "Gameplay/" + name || !sample.IsLoaded || Math.Abs(sample.Length - expected) > 2)
				throw new Exception("Wrong loaded sample after pointer click: " + sample.Name);
		}
	}

	public static IEnumerable<Action> Steps(Editor editor)
	{
		var composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
		var map = composer.Dependencies.Get<EditorBeatmap>();
		var clock = composer.Dependencies.Get<EditorClock>();
		var divisor = composer.Dependencies.Get<BindableBeatDivisor>();
		var first = composer.ChildrenOfType<DrawableShredEditorNote>().First();
		yield return () => { clock.Stop(); clock.Seek(0); composer.Playback.ReturnOnPause.Value = false; };

		foreach (bool recorder in new[] { false, true })
		foreach (string bank in new[] { "soft", "drum", "normal" })
		{
			var button = composer.ChildrenOfType<SampleBankTernaryButton>().Single(b => b.BankName == bank).NormalButton;
			yield return () =>
			{
				composer.StepRecorder.Enabled.Value = recorder;
				map.SelectedHitObjects.Clear();
				map.SelectedHitObjects.AddRange(map.HitObjects);
			};
			foreach (var step in click(button)) yield return step;
			yield return () =>
			{
				if (map.SelectedHitObjects.Count != map.HitObjects.Count)
					throw new Exception("Toolbar mouse click cleared the note selection");
				if (!map.HitObjects.All(n => ShredHitObjectComposer.EditableSamples(n).Any(s => s.Name == HitSampleInfo.HIT_NORMAL && s.Bank == bank)))
					throw new Exception("Real pointer click did not apply " + bank);
				checkLoadedSounds(first, bank, 1);
				Console.WriteLine($"PASS real pointer applies and loads {bank}, recorder={recorder}");
			};
		}

		// Repeatable CPU update measurements; these intentionally do not assert FPS.
		foreach (bool selected in new[] { false, true })
		foreach (bool moving in new[] { false, true })
		{
			yield return () =>
			{
				map.SelectedHitObjects.Clear();
				if (selected) map.SelectedHitObjects.AddRange(map.HitObjects);
				clock.Seek(5000); clock.Start();
			};
			yield return () =>
			{
				EditorUiProbe.FrameCosts.Clear(); EditorUiProbe.CaptureFrames = true;
				EditorUiProbe.FrameAction = moving ? () => apply(new MousePositionAbsoluteInput
				{
					Position = composer.Grid.ToScreenSpace(composer.Grid.Receiver(0, 2) + new Vector2((float)Math.Sin(clock.CurrentTime / 100) * 80, -40))
				}) : null;
			};
			for (int i = 0; i < 12; i++) yield return () => { };
			yield return () =>
			{
				EditorUiProbe.CaptureFrames = false; EditorUiProbe.FrameAction = null; clock.Stop();
				var frames = EditorUiProbe.FrameCosts.Order().ToArray();
				Console.WriteLine($"PERF notes={map.HitObjects.Count} selected={selected} mouse={moving} frames={frames.Length} p50={frames[frames.Length / 2]:F3} p95={frames[(int)(frames.Length * .95)]:F3} max={frames.Last():F3}");
			};
		}

		int additions = 0;
		foreach (var pair in new[] {
			(HitSampleInfo.HIT_WHISTLE, GlobalAction.EditorToggleWhistleSound),
			(HitSampleInfo.HIT_FINISH, GlobalAction.EditorToggleFinishSound),
			(HitSampleInfo.HIT_CLAP, GlobalAction.EditorToggleClapSound) })
		{
			var button = composer.ChildrenOfType<DrawableTernaryButton<GlobalAction>>().Single(b => b.Action == pair.Item2);
			foreach (var step in click(button)) yield return step;
			yield return () =>
			{
				additions++;
				if (map.SelectedHitObjects.Count != map.HitObjects.Count || !map.HitObjects.All(n => ShredHitObjectComposer.EditableSamples(n).Any(s => s.Name == pair.Item1)))
					throw new Exception("Pointer did not apply addition " + pair.Item1);
				checkLoadedSounds(first, "normal", 1 + additions);
				Console.WriteLine("PASS real pointer applies and loads " + pair.Item1);
			};
		}

		var lines = (Box[,])typeof(ShredEditorPlayfield).GetField("gridLines", private_fields)!.GetValue(composer.Grid)!;
		int normalLineCount = 0;
		yield return () => { clock.Seek(5000); divisor.SetArbitraryDivisor(4); };
		yield return () =>
		{
			normalLineCount = lines.Cast<Box>().Count(b => b != null && b.Alpha > 0);
			if (normalLineCount == 0 || lines.Cast<Box>().Count(b => b != null) >= 256)
				throw new Exception("Ordinary grid allocates excessive hidden lines");
			Console.WriteLine("PASS ordinary view has " + normalLineCount + " grid lines, without allocating 1536 drawables");
			divisor.SetArbitraryDivisor(48);
		};
		yield return () =>
		{
			if (lines.Cast<Box>().Count(b => b != null && b.Alpha > 0) <= normalLineCount)
				throw new Exception("Fine spacing did not expand the grid");
			divisor.SetArbitraryDivisor(4);
		};
		yield return () =>
		{
			if (lines.Cast<Box>().Count(b => b != null && b.Alpha > 0) != normalLineCount)
				throw new Exception("Fine grid lines remained visible after restoring spacing");
			Console.WriteLine("PASS fine spacing expands the grid and returning to 1/4 hides unused lines");
			// Seek and immediately query before the next frame: the time bound must
			// follow the clock rather than retain the previous absolute time window.
			clock.Seek(20000);
			foreach (int row in new[] { 0, 1 })
			{
				var hold = new ShredHitObject { StartTime = clock.CurrentTime - 1000, Duration = 100000, Row = row, Fret = 2 };
				var body = composer.Grid.ToScreenSpace(composer.Grid.PositionAt(hold.Column, clock.CurrentTime + 100));
				if (!composer.Grid.NoteContains(hold, body) || composer.Grid.EndHandleContains(hold, body))
					throw new Exception("A long sustain crossing the viewport lost its selectable body");
				var far = new ShredHitObject { StartTime = composer.Grid.LastVisibleTime + 10000, Duration = 1000, Row = row, Fret = 2 };
				if (composer.Grid.NoteContains(far, body) || composer.Grid.EndHandleContains(far, body))
					throw new Exception("Offscreen note remains hittable");
			}
			Console.WriteLine("PASS bounded hit-testing preserves crossing sustains in both rows after an immediate seek");
		};
	}
}
