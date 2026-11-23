using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Shred.Beatmaps;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Objects.Drawables;
using osu.Game.Rulesets.Shred.UI;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.GameplayTest;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;
using osuTK.Input;

internal partial class Revision54EditorRateChecks
{
	private IEnumerator<Func<bool>> runRevision68EditorSteps()
	{
		yield return () => ScreenStack?.CurrentScreen is MainMenu;
		Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(createFixture()), "v68.osz") });
		yield return () => import.IsCompleted; import.GetAwaiter().GetResult();
		var info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred");
		yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "v54-shred"))?.Ruleset.ShortName == "shred";
		Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info, refetch: true); Ruleset.Value = info.Ruleset;
		ScreenStack.Push(new EditorLoader());
		yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse;
		var editor = (Editor)ScreenStack.CurrentScreen;
		var composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
		var map = editor.Dependencies.Get<EditorBeatmap>();
		var clock = editor.Dependencies.Get<EditorClock>();
		var divisor = composer.Dependencies.Get<BindableBeatDivisor>();
		var container = (ShredBlueprintContainer)composer.BlueprintContainer;
		composer.StepRecorder.Enabled.Value = false; clock.Stop();
		map.BeginChange(); map.Clear();
		foreach (var g in map.ControlPointInfo.Groups.ToArray()) map.ControlPointInfo.RemoveGroup(g);
		map.ControlPointInfo.GroupAt(0, true).Add(new TimingControlPoint { BeatLength = 60000d / 137 });
		var notes = new[] {
			new ShredHitObject { StartTime = 1000, Fret = 1, HeadSnapDivisor = 4 },
			new ShredHitObject { StartTime = 1000, Row = 1, Fret = 1, HeadSnapDivisor = 4 },
			new ShredHitObject { StartTime = 1000, Fret = 2, Duration = 1000, HeadSnapDivisor = 4, TailSnapDivisor = 4 },
			new ShredHitObject { StartTime = 1000, Row = 1, Fret = 2, Duration = 1000, HeadSnapDivisor = 4, TailSnapDivisor = 4 }
		};
		map.AddRange(notes); map.EndChange(); yield return () => true;
		foreach (double time in new[] { 900d, 1000, 1000.01, 1002, 1500, 1000, 900, 1000 })
		{
			clock.Seek(time); yield return () => true; yield return () => true;
			foreach (var n in notes)
			{
				var d = editor.ChildrenOfType<DrawableShredEditorNote>().Single(d => d.HitObject == n);
				var v = d.ChildrenOfType<ShredNoteVisual>().Single();
				var h = (Drawable)typeof(ShredNoteVisual).GetField("head", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(v);
				var clip = (Container)typeof(DrawableShredEditorNote).GetField("clip", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(d);
				if (time <= 1000)
				{
					check(d.Alpha > .99 && clip.Alpha > .99 && h.Alpha > .99 && d.IsAlive, "short/LN head visible before and at receiver after seeking: " + n.Column + "/" + time);
					foreach (float y in new[] { .01f, .5f, .99f })
					{
						Vector2 inside = clip.ToLocalSpace(h.ToScreenSpace(new Vector2(h.DrawWidth / 2, h.DrawHeight * y)));
						check(inside.X >= 0 && inside.X <= clip.DrawWidth && inside.Y >= -.1 && inside.Y <= clip.DrawHeight + .1, "receptor mask retains the full head height");
					}
				}
				else check(clip.Alpha == 0 || h.Alpha == 0, "head hidden only after passing: " + n.Column + "/" + time);
			}
		}
		composer.SnapState.SnapMovedObjects.Value = true;
		foreach (bool native in new[] { false, true })
		{
			map.BeginChange(); map.Clear();
			foreach (var g in map.ControlPointInfo.Groups.ToArray()) map.ControlPointInfo.RemoveGroup(g);
			map.ControlPointInfo.GroupAt(123.4567, true).Add(new TimingControlPoint { BeatLength = 60000d / 137 });
			map.ControlPointInfo.GroupAt(4007.1234, true).Add(new TimingControlPoint { BeatLength = 60000d / 173 });
			double start = ShredGridTiming.Snap(map.ControlPointInfo, 4, 1000);
			var copied = new[] {
				new ShredHitObject { StartTime = start, Fret = 1, HeadSnapDivisor = 4 },
				new ShredHitObject { StartTime = ShredGridTiming.Snap(map.ControlPointInfo, 4, start + 60000d / 137 / 4), Fret = 2, HeadSnapDivisor = 4 }
			};
			map.AddRange(copied); map.EndChange(); map.SelectedHitObjects.Clear(); map.SelectedHitObjects.AddRange(copied); divisor.SetArbitraryDivisor(4);
			yield return () => true;
			if (native) typeof(Editor).GetMethod("Copy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, null);
			else container.ClipboardCommand(Key.C);
			double target = ShredGridTiming.Snap(map.ControlPointInfo, 4, 4300);
			clock.Seek(target); yield return () => true;
			if (native) typeof(Editor).GetMethod("Paste", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, null);
			else container.ClipboardCommand(Key.V);
			yield return () => true;
			foreach (var n in map.SelectedHitObjects.OfType<ShredHitObject>())
			{
				double snapped = ShredGridTiming.Snap(map.ControlPointInfo, 4, n.StartTime);
				check(n.StartTime == snapped, "paste is exactly on local grid: native=" + native + ", fret=" + n.Fret);
			}
		}

		// Actual editor Test clock at 50%, with and without pitch adjustment.
		// Exercise the same chord/LN release through the loaded gameplay field.
		foreach (bool pitched in new[] { true, false })
		{
			map.BeginChange(); map.Clear();
			foreach (var g in map.ControlPointInfo.Groups.ToArray()) map.ControlPointInfo.RemoveGroup(g);
			map.ControlPointInfo.GroupAt(0, true).Add(new TimingControlPoint { BeatLength = 500 });
			map.AddRange(new[] {
				new ShredHitObject { StartTime = 1000, Fret = 1, Duration = 1000, HeadSnapDivisor = 4, TailSnapDivisor = 4 },
				new ShredHitObject { StartTime = 1000, Fret = 2, Duration = 1000, HeadSnapDivisor = 4, TailSnapDivisor = 4 },
				new ShredHitObject { StartTime = 2160, Fret = 3, HeadSnapDivisor = 4 },
				new ShredHitObject { StartTime = 2160, Fret = 4, HeadSnapDivisor = 4 },
				new ShredHitObject { StartTime = 8000, Fret = 1, HeadSnapDivisor = 4 }
			}); map.EndChange();
			clock.Stop(); clock.Seek(0);
			editor.ChildrenOfType<PlaybackRateControl>().Single().Current.Value = .5;
			editor.ChildrenOfType<PlaybackPitchControl>().Single().ChildrenOfType<SwitchButton>().Single().Current.Value = pitched;
			yield return () => true;
			check((bool)typeof(Editor).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, null), "50% Test fixture saves before entering gameplay");
			editor.TestGameplay();
			yield return () => ScreenStack.CurrentScreen is EditorPlayer p && p.IsLoaded;
			var player = (EditorPlayer)ScreenStack.CurrentScreen;
			var field = player.ChildrenOfType<ShredPlayfield>().Single();
			var testClock = player.ChildrenOfType<GameplayClockContainer>().Single();
			yield return () => !testClock.IsPaused.Value && close(field.PlaybackRate, .5);
			var input = new InputState();
			void press(int key) => field.OnPressed(new KeyBindingPressEvent<osu.Game.Rulesets.Shred.ShredAction>(input, (osu.Game.Rulesets.Shred.ShredAction)key));
			void release(int key) => field.OnReleased(new KeyBindingReleaseEvent<osu.Game.Rulesets.Shred.ShredAction>(input, (osu.Game.Rulesets.Shred.ShredAction)key));
			var objects = field.AllHitObjects.OfType<DrawableShredHitObject>().ToArray();
			var first = objects.Where(n => n.HitObject.StartTime == 1000).ToArray();
			var next = objects.Where(n => n.HitObject.StartTime == 2160).ToArray();
			yield return () => field.Clock.CurrentTime >= 900;
			press(100); press(101);
			yield return () => field.Clock.CurrentTime >= 1000;
			press(105); press(106); yield return () => true;
			check(first.Length == 2 && first.All(n => n.HeadHit), "50% Test accepts the LN chord, pitched=" + pitched);
			yield return () => field.Clock.CurrentTime >= 2000;
			release(106);
			yield return () => field.Clock.CurrentTime >= 2050;
			check(first.All(n => n.Judged && n.Result.IsHit), "50% Test finishes the LNs successfully");
			check(next.Length == 2 && next.All(n => !n.HeadJudged), "50% Test has no premature miss after the LN/pick release");
			release(105); release(100); release(101);
			yield return () => field.Clock.CurrentTime >= 2160;
			press(102); press(103); press(105); press(106); yield return () => true;
			check(next.All(n => n.HeadHit), "50% Test next chord remains playable normally");
			release(105); release(106); release(102); release(103);
			player.Exit(); yield return () => ScreenStack.CurrentScreen == editor;
		}
	}
}
