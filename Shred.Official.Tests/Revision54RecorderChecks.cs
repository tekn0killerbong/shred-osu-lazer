using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Testing;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.UI;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Input;

internal static class Revision54RecorderChecks
{
	private static object invoke(object target, string method, params object[] args)
		=> target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args)!;
	private static InputState state(Key? key = null, Vector2? point = null)
	{
		InputState result = new InputState();
		if (key.HasValue) result.Keyboard.Keys.SetPressed(key.Value, true);
		if (point.HasValue) result.Mouse.Position = point.Value;
		return result;
	}
	private static void down(ShredStepRecorder recorder, Key key) => invoke(recorder, "OnKeyDown", new KeyDownEvent(state(key), key));
	private static void up(ShredStepRecorder recorder, Key key) => invoke(recorder, "OnKeyUp", new KeyUpEvent(state(), key));
	private static void check(bool ok, string label)
	{
		if (!ok) throw new Exception(label);
		Console.WriteLine("PASS v54 editor " + label);
	}
	public static IEnumerable<Action> Steps(Editor editor)
	{
		ShredHitObjectComposer composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
		ShredStepRecorder recorder = composer.StepRecorder;
		EditorBeatmap map = composer.Dependencies.Get<EditorBeatmap>();
		EditorClock clock = composer.Dependencies.Get<EditorClock>();
		BindableBeatDivisor divisor = composer.Dependencies.Get<BindableBeatDivisor>();
		IEditorChangeHandler history = composer.Dependencies.Get<IEditorChangeHandler>();
		void tool(ShredPracticeArticulation articulation)
		{
			composer.ChildrenOfType<HitObjectCompositionToolButton>()
				.Single(b => b.Tool is ShredCompositionTool t && t.Articulation == articulation).Select();
		}
		void clear()
		{
			recorder.Enabled.Value = false;
			map.SelectedHitObjects.Clear(); map.BeginChange();
			foreach (var note in map.HitObjects.ToArray()) map.Remove(note);
			map.EndChange();
			clock.Stop(); clock.Seek(1000);
			recorder.Enabled.Value = true;
		}
		yield return () => { clear(); divisor.SetArbitraryDivisor(4); composer.SetSelectTool(); };
		yield return () =>
		{
			check(recorder.HasFocus, "recorder has focus");
			foreach (Key key in new[] { Key.A, Key.Z, Key.K, Key.M }) { down(recorder, key); up(recorder, key); }
			check(map.HitObjects.Count == 0 && Math.Abs(clock.CurrentTimeAccurate - 1000) < 0.01, "Select blocks fret creation, picks and automatic stepping");
			tool(ShredPracticeArticulation.Pick);
		};
		yield return () =>
		{
			down(recorder, Key.A); down(recorder, Key.S); down(recorder, Key.Z);
			check(map.HitObjects.Count == 3 && map.HitObjects.Cast<ShredHitObject>().All(n => n.Articulation == ShredPracticeArticulation.Pick), "Pick tool records frets alone on both rows");
			up(recorder, Key.A); up(recorder, Key.S);
			check(Math.Abs(clock.CurrentTimeAccurate - 1000) < 0.01, "partial chord release stays on current step");
			up(recorder, Key.Z);
			check(Math.Abs(clock.CurrentTimeAccurate - 1125) < 0.01, "last fret release advances exactly one step");
			history.RestoreState(-1);
			check(map.HitObjects.Count == 0, "recorded chord is one undo action");
			history.RestoreState(1);
			check(map.HitObjects.Count == 3, "redo restores recorded chord");
			clear(); tool(ShredPracticeArticulation.HammerPull);
		};
		yield return () =>
		{
			down(recorder, Key.K); down(recorder, Key.M);
			check(map.HitObjects.Count == 0, "fingerpicks alone create no OPEN notes");
			down(recorder, Key.A); down(recorder, Key.Z);
			up(recorder, Key.K); down(recorder, Key.L); up(recorder, Key.L);
			check(map.HitObjects.Count == 2 && map.HitObjects.Cast<ShredHitObject>().All(n => n.Articulation == ShredPracticeArticulation.HammerPull), "picks before or after frets do not change Hammer-pull tool");
			up(recorder, Key.A); up(recorder, Key.Z);
			check(Math.Abs(clock.CurrentTimeAccurate - 1125) < 0.01, "held fingerpick does not delay final fret release");
			up(recorder, Key.M);
			check(Math.Abs(clock.CurrentTimeAccurate - 1125) < 0.01, "fingerpick release adds no second step");
			clear(); tool(ShredPracticeArticulation.Pick);
		};
		yield return () => { down(recorder, Key.A); down(recorder, Key.Z); };
		yield return () =>
		{
			check(map.HitObjects.Cast<ShredHitObject>().All(n => !n.IsLong), "holding frets alone leaves short notes");
			down(recorder, Key.Quote); up(recorder, Key.Quote);
			check(map.HitObjects.Cast<ShredHitObject>().All(n => Math.Abs(n.Duration - 125) < 0.01), "empty step creates both LN extensions");
		};
		yield return () =>
		{
			check(Math.Abs(composer.Grid.CurrentTime - 1125) < 0.5, "first empty step visibly moves grid to LN tail");
			up(recorder, Key.A);
			down(recorder, Key.Quote); up(recorder, Key.Quote);
			check(map.HitObjects.Cast<ShredHitObject>().Single(n => n.Row == 0).Duration == 125
				&& map.HitObjects.Cast<ShredHitObject>().Single(n => n.Row == 1).Duration == 250, "only still-held fret extends on next empty step");
			up(recorder, Key.Z);
			check(Math.Abs(clock.CurrentTimeAccurate - 1250) < 0.01, "LN final release stays at its tail");
			clear(); tool(ShredPracticeArticulation.Pick);
		};
		yield return () => { down(recorder, Key.A); down(recorder, Key.Quote); up(recorder, Key.Quote); };
		yield return () =>
		{
			check(Math.Abs(composer.Grid.CurrentTime - 1125) < 0.5, "immediate first empty step also scrolls before any second appui");
			down(recorder, Key.S); up(recorder, Key.S);
			check(map.HitObjects.Cast<ShredHitObject>().Single(n => n.Fret == 2).StartTime == 1125,
				"note entered after first empty step matches visible grid position");
			check(Math.Abs(clock.CurrentTimeAccurate - 1125) < 0.01, "partial release keeps first empty-step position");
			up(recorder, Key.A);
			check(map.HitObjects.Cast<ShredHitObject>().Single(n => n.Fret == 1).Duration == 125
				&& Math.Abs(clock.CurrentTimeAccurate - 1125) < 0.01, "final release stays at the LN end without an extra step");
			clear(); tool(ShredPracticeArticulation.Pick);
		};
		yield return () => { down(recorder, Key.A); composer.SetSelectTool(); };
		yield return () =>
		{
			up(recorder, Key.A); down(recorder, Key.Z); up(recorder, Key.Z);
			check(map.HitObjects.Count == 1 && Math.Abs(clock.CurrentTimeAccurate - 1000) < 0.01, "switching to Select ends gesture without phantom release step");
			clear(); tool(ShredPracticeArticulation.Pick);
		};
		yield return () =>
		{
			Vector2 p = composer.Grid.ToScreenSpace(composer.Grid.PositionAt(0, 1500));
			// The OPEN strip sits just outside the five fret columns.
			p = composer.Grid.ToScreenSpace(composer.Grid.PositionAt(1, 1500) - new Vector2(composer.Grid.NoteSize(1).X / 2 + 8 * composer.Grid.Unit, 0));
			ShredPlacementBlueprint placement = (ShredPlacementBlueprint)composer.BlueprintContainer.CurrentPlacement;
			placement.UpdateTimeAndPosition(p, 1500);
			invoke(placement, "OnMouseDown", new MouseDownEvent(state(point: p), MouseButton.Left));
			invoke(placement, "OnMouseUp", new MouseUpEvent(state(point: p), MouseButton.Left));
			check(map.HitObjects.Cast<ShredHitObject>().Single().Fret == 0, "left-click still places an OPEN note");
			check(Math.Abs(clock.CurrentTimeAccurate - 1000) < 0.01, "mouse placement still leaves grid at its original position");
			clear(); tool(ShredPracticeArticulation.HammerPull);
		};
		yield return () =>
		{
			map.BeginChange();
			map.Add(new ShredHitObject { StartTime = 1000, Fret = 1, Articulation = ShredPracticeArticulation.Pick, Duration = 500 });
			map.EndChange();
			down(recorder, Key.A); up(recorder, Key.A);
			ShredHitObject note = map.HitObjects.Cast<ShredHitObject>().Single();
			check(note.Articulation == ShredPracticeArticulation.HammerPull && note.Duration == 500, "recording over existing head follows chosen tool and retains its LN");
			clear(); composer.SetSelectTool();
			map.BeginChange(); map.Add(new ShredHitObject { StartTime = 1000, Duration = 1000, Articulation = ShredPracticeArticulation.Wham }); map.EndChange();
		};
		yield return () =>
		{
			ShredHitObject note = map.HitObjects.Cast<ShredHitObject>().Single();
			DrawableShredEditorNote drawable = composer.ChildrenOfType<DrawableShredEditorNote>().Single(n => n.HitObject == note);
			ShredNoteVisual visual = drawable.ChildrenOfType<ShredNoteVisual>().Single();
			Box body = (Box)typeof(ShredNoteVisual).GetField("body", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(visual)!;
			foreach (Vector2 screen in new[] { body.ScreenSpaceDrawQuad.TopLeft, body.ScreenSpaceDrawQuad.BottomLeft })
			{
				Vector2 p = composer.Grid.ToLocalSpace(screen);
				float expectedX = composer.Grid.WhamReceiver.X - composer.Grid.Skin.Shear(0) * (p.Y - composer.Grid.WhamReceiver.Y);
				check(Math.Abs(p.X - expectedX) < 0.05, "rendered wham left edge meets slanted judgement line");
			}
			Vector2 inside = composer.Grid.PositionAt(12, 1300) + new Vector2(0, composer.Grid.WhamSize.Y * 0.4f);
			check(composer.Grid.NoteContains(note, composer.Grid.ToScreenSpace(inside)), "thicker bar remains selectable along its upper/lower region");
			float whamDistance = (composer.Grid.PositionAt(12, 1500) - composer.Grid.PositionAt(12, 1000)).Length;
			float fretDistance = (composer.Grid.PositionAt(3, 1500) - composer.Grid.PositionAt(3, 1000)).Length;
			check(Math.Abs(whamDistance - fretDistance) < 0.01, "editor wham and frets keep equal travel speed");
			clock.Seek(1500);
		};
		yield return () =>
		{
			ShredHitObject note = map.HitObjects.Cast<ShredHitObject>().Single();
			DrawableShredEditorNote drawable = composer.ChildrenOfType<DrawableShredEditorNote>().Single(n => n.HitObject == note);
			check((drawable.Position - composer.Grid.WhamReceiver).Length < 0.01, "passed wham stays clipped at angled judgement edge");
			Vector2 p = composer.Grid.ToScreenSpace(composer.Grid.PositionAt(12, 1750));
			invoke(composer.BlueprintContainer, "OnMouseDown", new MouseDownEvent(state(point: p), MouseButton.Right));
			check(map.HitObjects.Count == 0 && Math.Abs(clock.CurrentTimeAccurate - 1500) < 0.01, "right-click deletes wham without moving editor");
		};
	}
}
