using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Input;
using osu.Framework.Input.StateChanges;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Timing;
using osu.Game.Screens.Edit.Timing.Multiselect;
using osu.Game.Screens.Menu;
using osuTK.Input;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

internal partial class Revision54EditorRateChecks
{
	private static object timingInput(object target, string method, object e)
		=> target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, new[] { e });
	private static void commitTimingText(FormTextBox form, string value)
	{
		TextBox text = form.ChildrenOfType<TextBox>().Single();
		text.Text = value;
		var handler = (TextBox.OnCommitHandler)typeof(FormTextBox).GetField("OnCommit", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
		handler.Invoke(text, true);
	}
	private static void commitTimingText(MixedTimeSignature form, string value)
	{
		form.TextBox.Text = value;
		form.OnCommit!.Invoke(form.TextBox, true);
	}

	private IEnumerator<Func<bool>> runTimingSteps()
	{
		yield return () => ScreenStack?.CurrentScreen is MainMenu;
		string fixturePath = createFixture();
		Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(fixturePath), "timing.osz") });
		yield return () => import.IsCompleted;
		import.GetAwaiter().GetResult();
		foreach (string mode in new[] { "osu", "taiko", "fruits", "mania", "shred" })
		{
			BeatmapInfo info = null;
			yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-" + mode))?.Ruleset.ShortName == mode;
			Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true);
			Ruleset.Value = info.Ruleset;
			ScreenStack.Push(new EditorLoader());
			yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse;
			Editor editor = (Editor)ScreenStack.CurrentScreen;
			EditorBeatmap map = editor.Dependencies.Get<EditorBeatmap>();
			EditorClock clock = editor.Dependencies.Get<EditorClock>();
			EditorChangeHandler history = (EditorChangeHandler)editor.Dependencies.Get<IEditorChangeHandler>();
			clock.Stop(); clock.Seek(5000);
			map.BeginChange();
			foreach (ControlPointGroup group in map.ControlPointInfo.Groups.ToArray()) map.ControlPointInfo.RemoveGroup(group);
			for (int i = 0; i < 5; i++)
			{
				map.ControlPointInfo.Add(i * 10000, new TimingControlPoint { BeatLength = 500 + 25 * i, TimeSignature = new TimeSignature(3 + i), OmitFirstBarLine = i % 2 == 0 });
				map.ControlPointInfo.GroupAt(i * 10000).Add(new EffectControlPoint { KiaiMode = i % 2 == 0, ScrollSpeed = 1 + i * 0.1 });
			}
			map.ControlPointInfo.GroupAt(25000, true).Add(new EffectControlPoint { KiaiMode = false, ScrollSpeed = 1.5 });
			global::osu.Game.Rulesets.Objects.HitObject hold = mode switch
			{
				"osu" => new global::osu.Game.Rulesets.Osu.Objects.Spinner { Duration = 1500 },
				"taiko" => new global::osu.Game.Rulesets.Taiko.Objects.Swell { Duration = 1500 },
				"fruits" => new global::osu.Game.Rulesets.Catch.Objects.BananaShower { Duration = 1500 },
				"mania" => new global::osu.Game.Rulesets.Mania.Objects.HoldNote { Duration = 1500, Column = 2 },
				_ => new ShredHitObject { Duration = 1500, Row = 1, Fret = 3 },
			};
			hold.StartTime = 19500;
			map.Add(hold);
			map.EndChange();
			editor.Mode.Value = EditorScreenMode.Timing;
			Console.WriteLine("TIMING opening " + mode);
			MultiTimingContent content = null;
			DateTime contentStarted = DateTime.UtcNow;
			yield return () =>
			{
				content = editor.ChildrenOfType<MultiTimingContent>().SingleOrDefault();
				if ((DateTime.UtcNow - contentStarted).TotalSeconds > 6)
				{
					var screen = editor.ChildrenOfType<TimingScreen>().SingleOrDefault();
					throw new Exception($"Content={content?.LoadState}, settings={content?.MultipleSettings.LoadState}, signature={content?.MultipleSettings.Signature.LoadState}, screen={screen?.LoadState}, main={screen?.MainContent.Children.Count}; hooks={string.Join(",", editor.ChildrenOfType<Drawable>().Where(d => d.GetType().Name.Contains("Multiselect")).Select(d => d.GetType().Name))}");
				}
				return content?.IsLoaded == true;
			};
			Console.WriteLine("TIMING content loaded " + mode);
			TimingPointSelection selection = content.Selection;
			MultiTimingSettings settings = content.MultipleSettings;
			MultiControlPointList list = content.ChildrenOfType<MultiControlPointList>().Single();
			void rowClick(double time, bool control = false, bool shift = false)
			{
				MultiControlPointTable.DrawableControlGroup row = content.ChildrenOfType<MultiControlPointTable.DrawableControlGroup>().Single(r => r.IsPresent && r.Current.Value.Time == time);
				InputState state = new InputState();
				state.Mouse.Position = row.ScreenSpaceDrawQuad.Centre;
				if (control) state.Keyboard.Keys.SetPressed(Key.ControlLeft, true);
				if (shift) state.Keyboard.Keys.SetPressed(Key.ShiftLeft, true);
				timingInput(row, "OnClick", new ClickEvent(state, MouseButton.Left));
			}
			void selectAll()
			{
				InputManager input = (InputManager)typeof(Drawable).GetMethod("GetContainingInputManager", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(list, null)!;
				new KeyboardKeyInput(Key.ControlLeft, true).Apply(input.CurrentState, input);
				new KeyboardKeyInput(Key.A, true).Apply(input.CurrentState, input);
				new KeyboardKeyInput(Key.A, false).Apply(input.CurrentState, input);
				new KeyboardKeyInput(Key.ControlLeft, false).Apply(input.CurrentState, input);
				check(selection.Groups.Count == map.ControlPointInfo.Groups.Count, mode + " Ctrl+A through real input routing selects all timing points");
			}
			void undo() => menuItems(editor).Single(i => i.Text.Value.Equals(global::osu.Game.Localisation.CommonStrings.Undo)).Action.Value.Invoke();
			void redo() => menuItems(editor).Single(i => i.Text.Value.Equals(global::osu.Game.Localisation.CommonStrings.Redo)).Action.Value.Invoke();
			DateTime rowsStarted = DateTime.UtcNow;
			yield return () =>
			{
				var rows = content.ChildrenOfType<MultiControlPointTable.DrawableControlGroup>().Where(r => r.IsPresent).ToArray();
				if ((DateTime.UtcNow - rowsStarted).TotalSeconds > 4) throw new Exception($"Rows {rows.Length}; groups {map.ControlPointInfo.Groups.Count}; size {content.DrawSize}; list {list.DrawSize}");
				return rows.Length >= 6;
			};
			Console.WriteLine("TIMING rows loaded " + mode);
			rowClick(10000);
			yield return () =>
			{
				if ((DateTime.UtcNow - rowsStarted).TotalSeconds > 4) throw new Exception("Selection " + string.Join(",", selection.Groups.Select(g => g.Time)));
				return selection.Groups.Count == 1 && selection.Groups[0].Time == 10000;
			};
			check(content.SingleSettings.IsPresent && !settings.IsPresent, mode + " single point keeps native settings and BPM");
			rowClick(30000, control: true);
			yield return () => selection.Groups.Count == 2 && settings.Signature.IsLoaded;
			check(selection.Groups.Select(g => g.Time).SequenceEqual(new[] { 10000d, 30000d }), mode + " Ctrl+click selects disjoint points");
			check(settings.IsPresent && !content.SingleSettings.IsPresent && !settings.ChildrenOfType<TapTimingControl>().Any(), mode + " multi settings shown without BPM");
			check(settings.Signature.Current.Value == "Mixed", mode + " differing time signatures display Mixed");
			check(settings.Signature.ChildrenOfType<global::osu.Game.Graphics.Sprites.OsuSpriteText>().Any(t => t.IsPresent && t.Text.ToString() == "/ 4"),
				mode + " Mixed time signature keeps its /4 denominator visible");
			rowClick(10000, control: true);
			yield return () => selection.Groups.Count == 1;
			check(selection.Groups[0].Time == 30000, mode + " Ctrl+click toggles selected point off");
			rowClick(10000);
			yield return () => selection.Groups.Count == 1 && selection.Groups[0].Time == 10000;
			rowClick(30000, shift: true);
			yield return () => selection.Groups.Count == 4;
			check(selection.Groups.Select(g => g.Time).SequenceEqual(new[] { 10000d, 20000d, 25000d, 30000d }), mode + " Shift+click selects contiguous range including effects-only group");
			rowClick(0, shift: true);
			yield return () => true;
			check(selection.Groups.Select(g => g.Time).SequenceEqual(new[] { 0d, 10000d, 20000d, 25000d, 30000d }), mode + " next Shift+click extends left from the farthest selected timing point");
			rowClick(40000, shift: true);
			yield return () => true;
			check(selection.Groups.Count == 6, mode + " next Shift+click extends right without losing the left edge");
			rowClick(10000, shift: true);
			yield return () => true;
			check(selection.Groups.Count == 6, mode + " Shift+click inside timing selection never shrinks it");
			rowClick(10000);
			yield return () => selection.Groups.Count == 1;
			rowClick(30000, shift: true);
			yield return () => selection.Groups.Count == 4;
			rowClick(40000, control: true);
			yield return () => selection.Groups.Count == 5;
			selectAll();
			check(selection.Groups.Count == 6, mode + " Ctrl+A selects every group");
			double[] beforeHoldTimes = selection.Groups.Select(g => g.Time).ToArray();
			InputManager pointer = pointerInput;
			DiscreteAdjustmentControl<double> adjust = settings.Offset.ChildrenOfType<DiscreteAdjustmentControl<double>>().Single();
			settings.ChildrenOfType<global::osu.Game.Graphics.Containers.OsuScrollContainer>().First().ScrollIntoView(settings.Offset, false);
			yield return () => true;
			new MousePositionAbsoluteInput { Position = adjust.ScreenSpaceDrawQuad.Centre + new osuTK.Vector2(adjust.DrawWidth * 0.1f, 0) }.Apply(pointer.CurrentState, pointer);
			DateTime pointerStarted = DateTime.UtcNow;
			yield return () =>
			{
				if ((DateTime.UtcNow - pointerStarted).TotalSeconds > 3) throw new Exception("Offset hover failed at " + adjust.ScreenSpaceDrawQuad.Centre + "; mouse=" + pointer.CurrentState.Mouse.Position + "; hovered=" + string.Join(",", editor.ChildrenOfType<Drawable>().Where(d => d.IsHovered).Select(d => d.GetType().Name)));
				return adjust.ChildrenOfType<Drawable>().Any(d => d.IsHovered);
			};
			new MouseButtonInput(MouseButton.Left, true).Apply(pointer.CurrentState, pointer);
			DateTime holdStarted = DateTime.UtcNow;
			yield return () => (DateTime.UtcNow - holdStarted).TotalSeconds > 1.5;
			new MouseButtonInput(MouseButton.Left, false).Apply(pointer.CurrentState, pointer);
			yield return () => true;
			check(selection.Groups.Count == 6 && selection.Groups[0].Time != beforeHoldTimes[0], mode + " holding real offset control keeps all selected points");
			check(editor.ChildrenOfType<global::osu.Game.Screens.Edit.Components.Timelines.Summary.Parts.GroupVisualisation>()
				.Count(g => g.IsAlive && g.Group.ControlPoints.OfType<TimingControlPoint>().Any()) == map.ControlPointInfo.TimingPoints.Count,
				mode + " every red summary marker remains visible after held offset");
			undo();
			check(selection.Groups.Select(g => g.Time).SequenceEqual(beforeHoldTimes), mode + " undo held offset restores selected points");
			yield return () => true;
			check(settings.SkipBarLine.Current.Value && settings.SkipBarLine.IsMixed && settings.Kiai.Current.Value && settings.Kiai.IsMixed,
				mode + " mixed boolean settings displayed without changing points");
			check(settings.TimingEnabled.Current.Value && settings.TimingEnabled.IsMixed && settings.TimingEnabled.MixedLabel.IsPresent,
				mode + " partially enabled timing displays checked toggle and Mixed beside it");
			check(settings.MoveObjects.ScreenSpaceDrawQuad.Centre.Y < settings.Offset.ScreenSpaceDrawQuad.Centre.Y
				&& settings.Offset.ScreenSpaceDrawQuad.Centre.Y < settings.Signature.ScreenSpaceDrawQuad.Centre.Y
				&& settings.Signature.ScreenSpaceDrawQuad.Centre.Y < settings.SkipBarLine.ScreenSpaceDrawQuad.Centre.Y,
				mode + " native timing control order is retained without BPM controls");
			foreach (Drawable control in new Drawable[] { settings.SkipBarLine.Toggle, settings.TimingEnabled, settings.Signature.TextBox })
				check(control.ScreenSpaceDrawQuad.AABBFloat.Left >= settings.ScreenSpaceDrawQuad.AABBFloat.Left
					&& control.ScreenSpaceDrawQuad.AABBFloat.Right <= settings.ScreenSpaceDrawQuad.AABBFloat.Right,
					mode + " toggle/signature fits inside settings width: " + control.GetType().Name);
			timingInput(settings.SkipBarLine.Toggle, "OnClick", new ClickEvent(new InputState(), MouseButton.Left));
			check(!settings.SkipBarLine.Current.Value && !settings.SkipBarLine.IsMixed && map.ControlPointInfo.TimingPoints.All(t => !t.OmitFirstBarLine),
				mode + " clicking mixed checked toggle turns the entire selection off");
			timingInput(settings.SkipBarLine.Toggle, "OnClick", new ClickEvent(new InputState(), MouseButton.Left));
			check(settings.SkipBarLine.Current.Value && !settings.SkipBarLine.IsMixed && map.ControlPointInfo.TimingPoints.All(t => t.OmitFirstBarLine),
				mode + " clicking unchecked toggle turns the entire selection on");
			undo(); undo();
			if (mode == "shred")
			{
				// Cross the history retention boundary, then revisit earlier offsets.
				for (int i = 1; i <= 130; i++)
				{
					settings.Offset.Current.Value = i;
					yield return () => true;
					check(selection.Groups.Count == 6, "extended offset editing retains selection #" + i);
				}
				settings.Offset.Current.Value = 100;
				yield return () => true;
				check(selection.Groups.Select(g => g.Time).SequenceEqual(beforeHoldTimes.Select(t => t + 100)), "revisiting prior offset keeps live selection");
				undo();
				check(selection.Groups.Select(g => g.Time).SequenceEqual(beforeHoldTimes.Select(t => t + 130)), "undo repeated offset retains full selection; selected="
					+ string.Join(",", selection.Groups.Select(g => g.Time)) + "; groups=" + string.Join(",", map.ControlPointInfo.Groups.Select(g => g.Time)));
				settings.Offset.Current.Value = 0;
				yield return () => true;
				check(selection.Groups.Select(g => g.Time).SequenceEqual(beforeHoldTimes), "returning to initial offset keeps all points selected");
			}
			string beforeSignature = history.CurrentStateHash;
			double[] tempos = map.ControlPointInfo.TimingPoints.Select(t => t.BeatLength).ToArray();
			commitTimingText(settings.Signature, "7/4");
			check(map.ControlPointInfo.TimingPoints.All(t => t.TimeSignature.Numerator == 7), mode + " mixed signature editable across selection");
			check(settings.Signature.Current.Value == "7/4", mode + " uniform signature shown after edit");
			undo();
			check(history.CurrentStateHash == beforeSignature && selection.Groups.Count == 6 && settings.Signature.Current.Value == "Mixed", mode + " one undo restores all signatures and selection");
			redo();
			check(map.ControlPointInfo.TimingPoints.All(t => t.TimeSignature.Numerator == 7), mode + " one redo restores batch signature");
			selection.Click(map.ControlPointInfo.GroupAt(10000), false, false);
			selection.Click(map.ControlPointInfo.GroupAt(30000), true, false);
			settings.TimingEnabled.Current.Value = false;
			check(map.ControlPointInfo.TimingPoints.Count == 3, mode + " timing presence editable across group");
			undo();
			check(map.ControlPointInfo.TimingPoints.Count == 5 && selection.Groups.Count == 2, mode + " undo restores timing presence and selection");
			bool[] effectPresence = selection.Groups.Select(g => g.ControlPoints.OfType<EffectControlPoint>().Any()).ToArray();
			settings.EffectsEnabled.Current.Value = false;
			check(selection.Groups.All(g => !g.ControlPoints.OfType<EffectControlPoint>().Any()), mode + " effects presence editable across group");
			undo();
			check(selection.Groups.Select(g => g.ControlPoints.OfType<EffectControlPoint>().Any()).SequenceEqual(effectPresence), mode + " undo restores effects");
			selection.Click(map.ControlPointInfo.GroupAt(20000), false, false);
			selection.Click(map.ControlPointInfo.GroupAt(25000), true, false);
			selection.SetTimingEnabled(true);
			check(map.ControlPointInfo.GroupAt(25000).ControlPoints.OfType<TimingControlPoint>().Single().BeatLength == 550,
				mode + " enabling timing inherits each section BPM");
			undo();
			check(!map.ControlPointInfo.GroupAt(25000).ControlPoints.OfType<TimingControlPoint>().Any(), mode + " undo timing creation");
			selectAll();
			settings.SkipBarLine.Current.Value = false;
			check(map.ControlPointInfo.TimingPoints.All(t => !t.OmitFirstBarLine), mode + " bulk Skip Bar Line off");
			settings.SkipBarLine.Current.Value = true;
			check(map.ControlPointInfo.TimingPoints.All(t => t.OmitFirstBarLine), mode + " bulk Skip Bar Line on");
			settings.Kiai.Current.Value = false;
			check(map.ControlPointInfo.EffectPoints.All(t => !t.KiaiMode), mode + " bulk Kiai off");
			check(settings.ScrollSpeed.IsPresent == info.Ruleset.CreateInstance().EditorShowScrollSpeed, mode + " scroll-speed visibility follows ruleset");
			if (settings.ScrollSpeed.IsPresent)
			{
				check(settings.ScrollSpeed.Caption.ToString() == "Scroll Speed (Mixed)", mode + " mixed scroll speeds displayed");
				settings.ScrollSpeed.Current.Value = 1.75;
				check(map.ControlPointInfo.EffectPoints.All(t => close(t.ScrollSpeed, 1.75)), mode + " bulk scroll speed");
			}
			string beforeInvalid = history.CurrentStateHash;
			commitTimingText(settings.Signature, "0/4");
			commitTimingText(settings.Signature, "3/8");
			check(history.CurrentStateHash == beforeInvalid, mode + " invalid signatures leave data unchanged");
			settings.TimingEnabled.Current.Value = false;
			check(map.ControlPointInfo.TimingPoints.Count == 5 && selection.Error.Length > 0, mode + " first timing group cannot lose timing");
			// Disjoint sections: compare each object's ORIGINAL timing section before moving.
			selection.Click(map.ControlPointInfo.GroupAt(10000), false, false);
			selection.Click(map.ControlPointInfo.GroupAt(30000), true, false);
			settings.MoveObjects.Current.Value = true;
			double[] oldObjects = map.HitObjects.Select(n => n.StartTime).ToArray();
			string beforeMove = history.CurrentStateHash;
			clock.Seek(5555);
			settings.Offset.Current.Value = 10125.375;
			check(selection.Groups.Select(g => g.Time).SequenceEqual(new[] { 10125.375d, 30125.375d }), mode + " offset shifts disjoint selection preserving gaps");
			for (int i = 0; i < oldObjects.Length; i++)
			{
				double expected = oldObjects[i];
				if (expected >= 10000 && expected < 20000 || expected >= 30000 && expected < 40000) expected += 125.375;
				check(close(map.HitObjects[i].StartTime, expected), mode + " object section moved exactly once #" + i);
			}
			check(map.HitObjects.OfType<IHasDuration>().Single(n => n.Duration > 0).Duration == 1500, mode + " long object crossing section boundary retains duration");
			undo();
			check(history.CurrentStateHash == beforeMove && selection.Groups.Select(g => g.Time).SequenceEqual(new[] { 10000d, 30000d }), mode + " undo offset restores points, notes and selection");
			check(close(clock.CurrentTimeAccurate, 5555), mode + " timing undo leaves grid stationary");
			redo();
			check(selection.Groups.Select(g => g.Time).SequenceEqual(new[] { 10125.375d, 30125.375d }), mode + " redo offset restores moved selection");
			undo();
			settings.MoveObjects.Current.Value = false;
			settings.Offset.Current.Value = 10050;
			check(map.HitObjects.Select(n => n.StartTime).SequenceEqual(oldObjects), mode + " move objects off keeps all notes fixed");
			undo();
			string beforeCollision = history.CurrentStateHash;
			settings.Offset.Current.Value = 20000;
			check(history.CurrentStateHash == beforeCollision && selection.Error.Length > 0 && close(settings.Offset.Current.Value, 10000), mode + " colliding offset rejected without merging or losing points");
			check(!selection.MoveBy(double.NaN, true) && history.CurrentStateHash == beforeCollision, mode + " non-finite offset rejected");
			selectAll();
			settings.MoveObjects.Current.Value = true;
			settings.Offset.Current.Value = -250;
			check(map.HitObjects.Select(n => n.StartTime).SequenceEqual(oldObjects.Select(t => t - 250)), mode + " whole-map shift moves each note once across adjacent boundaries");
			check(map.ControlPointInfo.TimingPoints.Select(t => t.BeatLength).SequenceEqual(tempos), mode + " all BPM values remain unchanged");
			undo();
			check(map.HitObjects.Select(n => n.StartTime).SequenceEqual(oldObjects), mode + " whole-map offset undo restores notes");
			selection.Click(map.ControlPointInfo.GroupAt(10000), false, false);
			selection.Click(map.ControlPointInfo.GroupAt(30000), true, false);
			selection.Delete();
			check(map.ControlPointInfo.GroupAt(10000) == null && map.ControlPointInfo.GroupAt(30000) == null, mode + " batch deletion removes chosen groups");
			undo();
			check(selection.Groups.Count == 2 && map.ControlPointInfo.GroupAt(10000) != null && map.ControlPointInfo.GroupAt(30000) != null,
				mode + " one undo restores deleted points and selection");
			selectAll();
			// Ctrl+A must remain a text operation when an input has focus.
			TextBox input = settings.Signature.ChildrenOfType<TextBox>().Single();
			var focus = (global::osu.Framework.Input.IFocusManager)typeof(Drawable).GetMethod("GetContainingFocusManager", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(input, null)!;
			focus.ChangeFocus(input);
			InputState ctrl = new InputState(); ctrl.Keyboard.Keys.SetPressed(Key.ControlLeft, true);
			check(!(bool)timingInput(list, "OnKeyDown", new KeyDownEvent(ctrl, Key.A)), mode + " Ctrl+A in text field is not intercepted");
			focus.ChangeFocus(null);
			// Save, return to compose and re-enter, then reopen the file.
			double[] effectProbes = new[] { 0d, 10000d, 20000d, 25000d, 30000d, 40000d };
			var expectedEffects = effectProbes.Select(t => (map.ControlPointInfo.EffectPointAt(t).KiaiMode, map.ControlPointInfo.EffectPointAt(t).ScrollSpeed)).ToArray();
			string expectedTiming = string.Join(";", map.ControlPointInfo.TimingPoints.Select(t => $"{t.Time}/{t.BeatLength}/{t.TimeSignature}/{t.OmitFirstBarLine}"));
			editor.Mode.Value = EditorScreenMode.Compose;
			DateTime composeStarted = DateTime.UtcNow;
			yield return () => (DateTime.UtcNow - composeStarted).TotalSeconds > 0.3;
			check(editor.ChildrenOfType<global::osu.Game.Screens.Edit.Components.Timelines.Summary.Parts.GroupVisualisation>()
				.Count(g => g.IsAlive && g.Group.ControlPoints.OfType<TimingControlPoint>().Any()) == map.ControlPointInfo.TimingPoints.Count,
				mode + " red summary markers survive undo, redo, whole-map move and return to compose; markers="
				+ string.Join(",", editor.ChildrenOfType<global::osu.Game.Screens.Edit.Components.Timelines.Summary.Parts.GroupVisualisation>().Select(g => g.Group.Time + ":" + g.IsAlive + ":" + g.LoadState))
				+ "; expected=" + map.ControlPointInfo.TimingPoints.Count);
			editor.Mode.Value = EditorScreenMode.Timing;
			yield return () => true;
			check(editor.ChildrenOfType<MultiTimingContent>().Count() == 1, mode + " repeated timing entry does not duplicate UI");
			menuItems(editor).Single(i => i.Text.Value.Equals(WebCommonStrings.ButtonsSave)).Action.Value.Invoke();
			yield return () => !hasUnsavedChanges(editor);
			editor.Exit();
			yield return () => ScreenStack.CurrentScreen is MainMenu;
			var saved = BeatmapManager.GetWorkingBeatmap(BeatmapManager.QueryBeatmap(b => b.ID == info.ID), refetch: true).Beatmap;
			check(string.Join(";", saved.ControlPointInfo.TimingPoints.Select(t => $"{t.Time}/{t.BeatLength}/{t.TimeSignature}/{t.OmitFirstBarLine}")) == expectedTiming, mode + " timing edits survive save and reload");
			check(effectProbes.Select(t => (saved.ControlPointInfo.EffectPointAt(t).KiaiMode, saved.ControlPointInfo.EffectPointAt(t).ScrollSpeed)).SequenceEqual(expectedEffects),
				mode + " effect settings survive save and reload");
			if (mode == "shred")
			{
				var reopenedInfo = BeatmapManager.QueryBeatmap(b => b.ID == info.ID);
				var playable = BeatmapManager.GetWorkingBeatmap(reopenedInfo, refetch: true).GetPlayableBeatmap(new ShredRuleset().RulesetInfo);
				check(reopenedInfo.Ruleset.ShortName == "shred" && reopenedInfo.Ruleset.OnlineID == -1 && playable.HitObjects.All(n => n is ShredHitObject),
					"Shred remains native in database and recovers authored notes from velocity-capable carrier");
				check(playable.HitObjects.OfType<ShredHitObject>().Single(n => n.Duration > 0).Duration == 1500,
					"Shred long note survives new carrier save/reload");
				var exporter = new global::osu.Game.Rulesets.Shred.Compatibility.ShredLegacyExport(new global::osu.Framework.Platform.NativeStorage(folder));
				typeof(global::osu.Game.Rulesets.Shred.Compatibility.ShredLegacyExport).GetMethod("MutateBeatmap", BindingFlags.Instance | BindingFlags.NonPublic)!
					.Invoke(exporter, new object[] { reopenedInfo.BeatmapSet, playable });
				playable.BeatmapInfo.DifficultyName = "v56-export-shred";
				string archivePath = Path.Combine(folder, "v56-export.osz");
				using (var archive = System.IO.Compression.ZipFile.Open(archivePath, System.IO.Compression.ZipArchiveMode.Create))
				{
					using (var writer = new StreamWriter(archive.CreateEntry("shred.osu").Open()))
						new global::osu.Game.Beatmaps.Formats.LegacyBeatmapEncoder(playable, null, null).Encode(writer);
					using var fixture = System.IO.Compression.ZipFile.OpenRead(fixturePath);
					using Stream audio = fixture.GetEntry("tone.wav")!.Open();
					using Stream destination = archive.CreateEntry("tone.wav").Open();
					audio.CopyTo(destination);
				}
				Task imported = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(archivePath), "v56-export.osz") });
				yield return () => imported.IsCompleted;
				imported.GetAwaiter().GetResult();
				BeatmapInfo reimported = null;
				yield return () => (reimported = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v56-export-shred"))?.Ruleset.ShortName == "shred";
				IBeatmap roundtrip = BeatmapManager.GetWorkingBeatmap(reimported, refetch: true).GetPlayableBeatmap(new ShredRuleset().RulesetInfo);
				check(roundtrip.HitObjects.Count == playable.HitObjects.Count && roundtrip.HitObjects.Cast<ShredHitObject>().Single(n => n.Duration > 0).Row == 1,
					"official exported osz automatically reimports as native Shred with authored rows and LN");
				check(effectProbes.Select(t => roundtrip.ControlPointInfo.EffectPointAt(t).ScrollSpeed).SequenceEqual(expectedEffects.Select(e => e.ScrollSpeed)),
					"official exported osz preserves all effect scroll speeds");
				Beatmap.Value = BeatmapManager.GetWorkingBeatmap(reimported, refetch: true);
				Ruleset.Value = reimported.Ruleset;
				ScreenStack.Push(new EditorLoader());
				yield return () => ScreenStack.CurrentScreen is Editor reopenedEditor && reopenedEditor.ReadyForUse;
				Editor reopenedEditor = (Editor)ScreenStack.CurrentScreen;
				check(reopenedEditor.Dependencies.Get<EditorBeatmap>().HitObjects.All(n => n is ShredHitObject), "reimported export opens the Shred editor");
				foreach (Func<bool> step in duplicateSteps(reopenedEditor)) yield return step;
				foreach (Func<bool> step in tailInsertionSteps(reopenedEditor)) yield return step;
				foreach (Func<bool> step in emptyNavigationSteps(reopenedEditor)) yield return step;
				foreach (Func<bool> step in rangeSelectionSteps(reopenedEditor)) yield return step;
				menuItems(reopenedEditor).Single(i => i.Text.Value.Equals(WebCommonStrings.ButtonsSave)).Action.Value.Invoke();
				yield return () => !hasUnsavedChanges(reopenedEditor);
				reopenedEditor.Exit();
				yield return () => ScreenStack.CurrentScreen is MainMenu;
			}
		}
	}
}
