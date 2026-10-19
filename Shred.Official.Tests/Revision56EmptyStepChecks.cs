using osu.Framework.Allocation;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.StateChanges;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using RulesetType = osu.Game.Rulesets.Ruleset;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osuTK.Input;

internal partial class Revision54EditorRateChecks
{
	private IEnumerable<Func<bool>> emptyNavigationSteps(Editor editor)
	{
		ShredHitObjectComposer composer = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
		EditorBeatmap map = editor.Dependencies.Get<EditorBeatmap>();
		EditorClock clock = editor.Dependencies.Get<EditorClock>();
		BindableBeatDivisor divisor = composer.Dependencies.Get<BindableBeatDivisor>();
		RealmAccess realm = editor.Dependencies.Get<RealmAccess>();
		void key(Key value, bool down) => new KeyboardKeyInput(value, down).Apply(pointerInput.CurrentState, pointerInput);
		void tap(Key value) { key(value, true); key(value, false); }
		void reset()
		{
			composer.StepRecorder.Enabled.Value = false;
			clock.Stop(); clock.Seek(1000);
			divisor.SetArbitraryDivisor(4);
			map.BeginChange();
			foreach (var note in map.HitObjects.ToArray()) map.Remove(note);
			foreach (ControlPointGroup group in map.ControlPointInfo.Groups.ToArray()) map.ControlPointInfo.RemoveGroup(group);
			map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
			map.EndChange();
			composer.ChildrenOfType<HitObjectCompositionToolButton>()
				.Single(b => b.Tool is ShredCompositionTool tool && tool.Articulation == ShredPracticeArticulation.Pick).Select();
			composer.StepRecorder.Enabled.Value = true;
		}
		realm.Run(r => check(r.All<RealmKeyBinding>().Any(b => b.RulesetName == "shred" && b.Variant == RulesetType.EDITOR_VARIANT
			&& b.ActionInt == (int)ShredEditorAction.EmptyStep && b.KeyCombinationString == new KeyCombination(InputKey.Right).ToString()),
			"Right Empty step has a persisted slot on the shared action"));
		foreach (Key empty in new[] { Key.Slash, Key.Right })
		{
			reset();
			yield return () => composer.StepRecorder.HasFocus;
			key(Key.A, true); key(Key.Z, true);
			yield return () => true;
			check(map.HitObjects.Count == 2 && map.HitObjects.Cast<ShredHitObject>().All(n => n.Duration == 0), empty + " held frets alone stay short");
			tap(empty);
			// The scheduler can catch up several ticks within one host frame.
			// Wait for an actual clock update, without sending a second empty step.
			DateTime firstStepDeadline = DateTime.UtcNow.AddMilliseconds(200);
			yield return () => composer.Grid.CurrentTime > 1001 || DateTime.UtcNow >= firstStepDeadline;
			check(close(clock.CurrentTimeAccurate, 1125) && composer.Grid.CurrentTime > 1001 && composer.Grid.CurrentTime <= 1125.5
				&& map.HitObjects.Cast<ShredHitObject>().All(n => close(n.Duration, 125)), empty + " first empty step extends both held frets and visibly moves grid once; clock="
				+ clock.CurrentTimeAccurate + "; grid=" + composer.Grid.CurrentTime + "; notes=" + string.Join(",", map.HitObjects.Cast<ShredHitObject>().Select(n => n.StartTime + ":" + n.Duration)));
			DateTime movementStarted = DateTime.UtcNow;
			yield return () => (DateTime.UtcNow - movementStarted).TotalSeconds > 0.3;
			check(Math.Abs(composer.Grid.CurrentTime - 1125) < 0.5, empty + " smooth grid movement completes at first empty step");
			key(Key.A, false);
			tap(empty);
			yield return () => true;
			check(close(clock.CurrentTimeAccurate, 1250) && map.HitObjects.Cast<ShredHitObject>().Single(n => n.Row == 0).Duration == 125
				&& map.HitObjects.Cast<ShredHitObject>().Single(n => n.Row == 1).Duration == 250, empty + " extends only still-held fret");
			key(Key.Z, false);
			check(close(clock.CurrentTimeAccurate, 1250)
				&& map.HitObjects.Cast<ShredHitObject>().Single(n => n.Row == 1).EndTime == 1250,
				empty + " final release stays at the LN end without another step");
			DateTime releaseStarted = DateTime.UtcNow;
			yield return () => (DateTime.UtcNow - releaseStarted).TotalSeconds > 0.3;
			check(Math.Abs(composer.Grid.CurrentTime - 1250) < 0.5, empty + " visible grid settles at the LN end after release");
			tap(empty);
			check(close(clock.CurrentTimeAccurate, 1375) && map.HitObjects.Count == 2, empty + " no held fret advances without creating a note");
			key(Key.A, true); key(Key.A, false);
			check(close(clock.CurrentTimeAccurate, 1500) && map.HitObjects.Cast<ShredHitObject>().Any(n => n.StartTime == 1375 && n.Duration == 0),
				empty + " the following short-note gesture still advances on release");
		}
		realm.Write(r => r.All<RealmKeyBinding>().Single(b => b.RulesetName == "shred" && b.Variant == RulesetType.EDITOR_VARIANT
			&& b.ActionInt == (int)ShredEditorAction.EmptyStep && b.KeyCombinationString == new KeyCombination(InputKey.Right).ToString()).KeyCombination = new KeyCombination(InputKey.T));
		DateTime rebound = DateTime.UtcNow;
		yield return () => (DateTime.UtcNow - rebound).TotalSeconds > 0.3;
		reset();
		yield return () => composer.StepRecorder.HasFocus;
		key(Key.A, true); tap(Key.T);
		check(map.HitObjects.Cast<ShredHitObject>().Single().Duration == 125 && close(clock.CurrentTimeAccurate, 1125), "custom navigation binding T replaces Right for Empty step");
		key(Key.A, false);
		check(close(clock.CurrentTimeAccurate, 1125), "rebound Empty step also stays at LN end on release");
		reset();
		yield return () => composer.StepRecorder.HasFocus;
		key(Key.A, true); tap(Key.Right);
		check(map.HitObjects.Cast<ShredHitObject>().Single().Duration == 0 && close(clock.CurrentTimeAccurate, 1125), "unbound Right reverts to navigation without extending LN");
		key(Key.A, false);
		realm.Write(r => r.All<RealmKeyBinding>().Single(b => b.RulesetName == "shred" && b.Variant == RulesetType.EDITOR_VARIANT
			&& b.ActionInt == (int)ShredEditorAction.EmptyStep && b.KeyCombinationString == new KeyCombination(InputKey.T).ToString()).KeyCombination = new KeyCombination(InputKey.Right));
		composer.StepRecorder.Enabled.Value = false;
	}
}
