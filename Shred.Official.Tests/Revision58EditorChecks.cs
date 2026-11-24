using osu.Framework.Allocation;
using osu.Framework.Input.StateChanges;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Mods;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.UI;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.GameplayTest;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Input;

internal partial class Revision54EditorRateChecks
{
	private IEnumerator<Func<bool>> runRevision58Steps()
	{
		yield return () => ScreenStack?.CurrentScreen is MainMenu;
		Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(createFixture()), "v58.osz") });
		yield return () => import.IsCompleted;
		import.GetAwaiter().GetResult();
		bool rememberedPitch = true;
		foreach (string mode in new[] { "osu", "taiko", "fruits", "mania", "shred", "shred" })
		{
			BeatmapInfo info = null;
			yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-" + mode))?.Ruleset.ShortName == mode;
			Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true);
			Ruleset.Value = info.Ruleset;
			ScreenStack.Push(new EditorLoader());
			yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse && e.ChildrenOfType<PlaybackRateControl>().Any(c => c.IsLoaded);
			Editor editor = (Editor)ScreenStack.CurrentScreen;
			EditorClock editorClock = editor.Dependencies.Get<EditorClock>();
			PlaybackRateControl control = editor.ChildrenOfType<PlaybackRateControl>().Single();
			PlaybackPitchControl header = editor.ChildrenOfType<PlaybackPitchControl>().Single();
			RoundedSliderBar<double> slider = control.ChildrenOfType<RoundedSliderBar<double>>().Single();
			OsuSpriteText percentage = control.ChildrenOfType<OsuSpriteText>().Single();
			SwitchButton toggle = header.ChildrenOfType<SwitchButton>().Single();
			yield return () => slider.DrawWidth > 50 && header.DrawWidth > 0;
			yield return () => true;
			check(close(control.Current.Value, 1) && close(control.Current.Default, 1), mode + " each editor opens at 100%, including reopening the same map");
			check(toggle.Current.Value == rememberedPitch, mode + " Pitched remains independently remembered");
			check(editor.ChildrenOfType<PlaybackControl.PlaybackTabControl>().All(tabs => !tabs.IsPresent), mode + " old percentage buttons are hidden");
			checkPlaybackAlignment(editor, mode);
			check(percentage.ScreenSpaceDrawQuad.TopLeft.X > slider.ScreenSpaceDrawQuad.BottomRight.X, mode + " numeric percentage is on the slider's right");
			check(header.ScreenSpaceDrawQuad.BottomRight.Y <= control.ScreenSpaceDrawQuad.TopLeft.Y + 1, mode + " slider fits below Playback speed | Pitched");

			revision57Click(slider.ToScreenSpace(new Vector2(0, slider.DrawHeight / 2)));
			yield return () => true;
			check(close(control.Current.Value, 0.01) && percentage.Text.ToString() == "1%", mode + " left endpoint selects and displays 1%");
			revision57Click(slider.ToScreenSpace(new Vector2(slider.DrawWidth - 1, slider.DrawHeight / 2)));
			yield return () => true;
			check(close(control.Current.Value, 2) && percentage.Text.ToString() == "200%", mode + " right endpoint selects and displays 200%, got " + control.Current.Value + ", " + percentage.Text);
			revision57Tap(Key.Left);
			yield return () => true;
			check(close(control.Current.Value, 1.99), mode + " focused slider keyboard adjustment is one percent");
			// A real drag must update both the control and audio, not just the label.
			new MousePositionAbsoluteInput { Position = slider.ToScreenSpace(new Vector2(0, slider.DrawHeight / 2)) }.Apply(pointerInput.CurrentState, pointerInput);
			new MouseButtonInput(MouseButton.Left, true).Apply(pointerInput.CurrentState, pointerInput);
			yield return () => true;
			new MousePositionAbsoluteInput { Position = slider.ToScreenSpace(new Vector2(slider.DrawWidth * 0.6f, slider.DrawHeight / 2)) }.Apply(pointerInput.CurrentState, pointerInput);
			yield return () => true;
			new MouseButtonInput(MouseButton.Left, false).Apply(pointerInput.CurrentState, pointerInput);
			yield return () => true;
			check(control.Current.Value > 1 && control.Current.Value < 1.7, mode + " dragging selects an intermediate value");

			foreach (bool pitched in new[] { true, false })
			foreach (double rate in new[] { 0.01, 0.05, 0.25, 0.5, 0.75, 1, 1.37, 2 })
			{
				editorClock.Stop(); editorClock.Seek(5000);
				toggle.Current.Value = pitched;
				control.Current.Value = rate;
				yield return () => true;
				check(percentage.Text.ToString() == $"{rate * 100:0}%", mode + " live numeric value " + rate);
				double tempo = rate;
				double frequency = 1;
				if (pitched) { tempo = 1; frequency = rate; }
				else if (rate < 0.05) { tempo = 0.05; frequency = rate / 0.05; }
				check(close(editorClock.AudioAdjustments.AggregateTempo.Value, tempo) && close(editorClock.AudioAdjustments.AggregateFrequency.Value, frequency), mode + " selected rate and Pitched update editor adjustments");
				editorClock.Start();
				yield return () => close(((IClock)editorClock).Rate, rate);
				check(close(Beatmap.Value.Track.AggregateTempo.Value, tempo) && close(Beatmap.Value.Track.AggregateFrequency.Value, frequency), mode + " audio track rate " + rate + ", pitched=" + pitched);
				if (mode == "shred") checkEditorAbsoluteScroll58(editor, rate);
				editorClock.Stop();
				if (mode == "shred") checkEditorAbsoluteScroll58(editor, rate);
				editor.TestGameplay();
				yield return () => ScreenStack.CurrentScreen is EditorPlayer p && p.IsLoaded;
				EditorPlayer player = (EditorPlayer)ScreenStack.CurrentScreen;
				GameplayClockContainer clock = player.ChildrenOfType<GameplayClockContainer>().Single();
				yield return () => !clock.IsPaused.Value && close(((IClock)clock).Rate, rate);
				check(close(clock.AdjustmentsFromMods.AggregateTempo.Value, tempo)
					&& close(clock.AdjustmentsFromMods.AggregateFrequency.Value, frequency), mode + " Test exact audio factors at " + rate + ", pitched=" + pitched);
				if (mode == "shred")
				{
					ShredPlayfield field = player.ChildrenOfType<ShredPlayfield>().Single();
					yield return () => close(field.PlaybackRate, rate);
					checkGameplayAbsoluteScroll58(field, rate, "Test");
				}
				player.Exit();
				yield return () => ScreenStack.CurrentScreen == editor;
				check(close(control.Current.Value, rate) && toggle.Current.Value == pitched, mode + " Test return preserves the current session rate and pitch");
				checkClock(clock, 1, mode + " Test exit cleans up audio adjustments");
			}
			control.Current.Value = 1.37;
			editor.Mode.Value = EditorScreenMode.Timing;
			yield return () => control.Current.Disabled;
			check(close(control.Current.Value, 1.37), mode + " timing mode preserves the selected speed");
			revision57Click(slider.ScreenSpaceDrawQuad.Centre);
			yield return () => true;
			check(close(control.Current.Value, 1.37), mode + " disabled timing slider preserves value on clicks");
			editor.Mode.Value = EditorScreenMode.Compose;
			yield return () => !control.Current.Disabled;
			control.Current.Value = 1.37;
			rememberedPitch = !rememberedPitch;
			toggle.Current.Value = rememberedPitch;
			editor.Exit();
			yield return () => ScreenStack.CurrentScreen is MainMenu;
		}
		foreach (Func<bool> step in runRevision58Mods()) yield return step;
	}

	private void checkEditorAbsoluteScroll58(Editor editor, double rate)
	{
		ShredEditorPlayfield field = editor.ChildrenOfType<ShredEditorPlayfield>().Single();
		EditorBeatmap map = editor.Dependencies.Get<EditorBeatmap>();
		double reference = ShredEditorOperations.VisibleTime(map.TimelineZoom);
		check(close(field.PlaybackRate, rate) && close(field.VisibleTime, reference), "editor zoom is independent of playback rate " + rate);
		foreach (int column in new[] { 1, 8, 12 })
		{
			double time = field.CurrentTime + 0.2 * reference;
			Vector2 position = field.PositionAt(column, time);
			check(Math.Abs(field.TimeAt(field.ToScreenSpace(position), column) - time) < 0.05, "editor placement inverse remains correct at " + rate + ", column=" + column);
		}
	}

	private void checkGameplayAbsoluteScroll58(ShredPlayfield field, double rate, string label)
	{
		check(close(field.MapApproachTime / rate, ShredPlayfield.ApproachTime), label + " displayed scroll duration stays absolute at " + rate);
		foreach (int column in new[] { 1, 8, 12 })
		{
			ShredHitObject note = new ShredHitObject { Row = column / 6 % 2, Fret = column % 6 };
			if (column == 12) note.Articulation = ShredPracticeArticulation.Wham;
			Vector2 expected = field.Receiver(note.Row, note.Fret) - field.LaneTravel(note.Row, note.Fret);
			if (column == 12) expected = field.WhamReceiver + new Vector2(field.WhamTravel, 0);
			Vector2 actual = field.PositionAt(note, field.Clock.CurrentTime + ShredPlayfield.ApproachTime * rate);
			check((actual - expected).Length < 0.01, label + " real-time full traversal at " + rate + ", column=" + column);
		}
	}

	private IEnumerable<Func<bool>> runRevision58Mods()
	{
		foreach (bool pitched in new[] { false, true })
		foreach (double rate in new[] { 0.5, 0.75, 0.99, 1.01, 1.5, 2 })
		{
			ModRateAdjust mod;
			if (rate < 1)
				mod = new ShredModHalfTime { SpeedChange = { Value = rate }, AdjustPitch = { Value = pitched } };
			else
				mod = new ShredModDoubleTime { SpeedChange = { Value = rate }, AdjustPitch = { Value = pitched } };
			SelectedMods.Value = new Mod[] { new ShredModNoFail(), mod };
			ScreenStack.Push(new PlayerLoader(() => new SoloPlayer(new PlayerConfiguration { ShowResults = false, AutomaticallySkipIntro = true })));
			yield return () => ScreenStack.CurrentScreen is Player p && p.IsLoaded;
			Player player = (Player)ScreenStack.CurrentScreen;
			GameplayClockContainer clock = player.ChildrenOfType<GameplayClockContainer>().Single();
			ShredPlayfield field = player.ChildrenOfType<ShredPlayfield>().Single();
			yield return () => !clock.IsPaused.Value && close(field.PlaybackRate, rate);
			checkClock(clock, rate, mod.Acronym + " actual gameplay", pitched);
			checkGameplayAbsoluteScroll58(field, rate, mod.Acronym);
			clock.Stop();
			yield return () => clock.IsPaused.Value;
			checkGameplayAbsoluteScroll58(field, rate, mod.Acronym + " paused");
			player.Exit();
			yield return () => ScreenStack.CurrentScreen is MainMenu;
		}
		SelectedMods.Value = Array.Empty<Mod>();
	}
}
