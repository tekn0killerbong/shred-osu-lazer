using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Graphics.UserInterface;
using System.Reflection;
using System.Text;
using osu.Framework.Allocation;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components.TernaryButtons;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK.Input;

internal static class Revision46UiChecks
{
	private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS v46 " + label); }
	private static bool KeyDown(ShredStepRecorder recorder, Key key)
	{
		var state = new InputState(); state.Keyboard.Keys.SetPressed(key, true);
		return (bool)typeof(ShredStepRecorder).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!
			.Invoke(recorder, new object[] { new KeyDownEvent(state, key) });
	}
	public static IEnumerable<Action> Steps(Editor editor)
	{
		var c = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
		var map = c.Dependencies.Get<EditorBeatmap>(); var clock = c.Dependencies.Get<EditorClock>();
		var history = c.Dependencies.Get<IEditorChangeHandler>(); var realm = c.Dependencies.Get<RealmAccess>();
		var divisor = c.Dependencies.Get<BindableBeatDivisor>();
		IEnumerable<ShredHitObject> Selected() => map.HitObjects.Cast<ShredHitObject>().Where(n => n.StartTime < 4000);
		IList<HitSampleInfo> Samples(ShredHitObject n) => ShredHitObjectComposer.EditableSamples(n);
		SampleBankTernaryButton Bank(string name) => c.ChildrenOfType<SampleBankTernaryButton>().Single(b => b.BankName == name);
		void Toggle(GlobalAction action) => c.ChildrenOfType<DrawableTernaryButton<GlobalAction>>().Single(b => b.Action == action).TriggerClick();
		yield return () =>
		{
			clock.Stop(); c.StepRecorder.Enabled.Value = false; map.BeginChange();
			foreach (var n in map.HitObjects.ToArray()) map.Remove(n);
			for (int i = 1; i <= 4; i++) map.Add(new ShredHitObject
			{
				StartTime = i * 1000, Fret = i, Duration = i == 2 ? 500 : 0,
				Articulation = i == 3 ? ShredPracticeArticulation.Wham : ShredPracticeArticulation.Pick,
				Samples = new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, "normal") }
			});
			map.EndChange(); map.SelectedHitObjects.AddRange(Selected());
		};
		yield return () => { Bank("soft").NormalButton.TriggerClick(); };
		yield return () =>
		{
			Check(Selected().All(n => Samples(n).Single(s => s.Name == HitSampleInfo.HIT_NORMAL).Bank == "soft"), "click Soft changes selected short, LN and wham normal banks");
			Check(Samples((ShredHitObject)map.HitObjects.Last()).Single().Bank == "normal", "unselected note is untouched");
			Check(c.SelectionBankStates["soft"].Value == TernaryState.True, "Soft button stays selected after native update");
			Bank("drum").AdditionsButton.TriggerClick();
		};
		foreach (var action in new[] { GlobalAction.EditorToggleWhistleSound, GlobalAction.EditorToggleFinishSound, GlobalAction.EditorToggleClapSound })
			yield return () => { Toggle(action); };
		yield return () =>
		{
			Check(Selected().All(n => Samples(n).Count == 4 && Samples(n).Where(s => s.Name != HitSampleInfo.HIT_NORMAL).All(s => s.Bank == "drum" && !s.EditorAutoBank)), "Whistle Finish Clap clicks add all three effects with chosen addition bank");
			foreach (var n in Selected()) Check(c.ChildrenOfType<DrawableShredEditorNote>().Single(d => d.HitObject == n).GetSamples().SelectMany(s => s.LookupNames.Take(1)).SequenceEqual(Samples(n).SelectMany(s => s.LookupNames.Take(1))), "playback samples reflect edited bank/effects");
			Bank("auto").AdditionsButton.TriggerClick();
		};
		yield return () => { Bank("drum").NormalButton.TriggerClick(); };
		yield return () => { Check(Selected().All(n => Samples(n).All(s => s.Bank == "drum")), "Auto additions follow normal bank change"); Toggle(GlobalAction.EditorToggleClapSound); };
		yield return () => { Check(Selected().All(n => Samples(n).All(s => s.Name != HitSampleInfo.HIT_CLAP)), "effect can be toggled off on entire selection"); history.RestoreState(-1); };
		yield return () => { Check(Selected().All(n => Samples(n).Any(s => s.Name == HitSampleInfo.HIT_CLAP)), "undo restores removed effect"); history.RestoreState(1); };
		yield return () =>
		{
			Check(Selected().All(n => Samples(n).Count == 3 && Samples(n).All(s => s.Bank == "drum")), "redo preserves edited bank and effects");
			using var writer = new StringWriter(); new LegacyBeatmapEncoder(map, map.BeatmapSkin, map.Storyboard).Encode(writer);
			using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(writer.ToString())); using var reader = new LineBufferedReader(bytes);
			var decoded = new LegacyBeatmapDecoder(128).Decode(reader);
			var reloaded = new ShredRuleset().CreateBeatmapConverter(decoded).Convert().HitObjects.Cast<ShredHitObject>().ToArray();
			Check(reloaded.Take(3).All(n => Samples(n).Count == 3 && Samples(n).All(s => s.Bank == "drum")), "native encode/decode retains edited banks and effects");
			Check(reloaded[1].Duration == 500 && reloaded[2].IsWham && reloaded.Select(n => n.Column).SequenceEqual(map.HitObjects.Cast<ShredHitObject>().Select(n => n.Column)), "sample edits retain Shred geometry and LN duration");
			Check((bool)typeof(Editor).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(editor, null), "editor saves hitsound edits successfully");
		};
		foreach (bool enabled in new[] { false, true })
		{
			yield return () => { c.StepRecorder.Enabled.Value = enabled; divisor.ValidDivisors.Value = BeatDivisorPresetCollection.COMMON; divisor.SetArbitraryDivisor(4); };
			yield return () =>
			{
				Check(KeyDown(c.StepRecorder, Key.Q) && divisor.Value == 2, "Q increases spacing, recorder=" + enabled);
				Check(KeyDown(c.StepRecorder, Key.W) && divisor.Value == 4, "W decreases spacing, recorder=" + enabled);
				Check(KeyDown(c.StepRecorder, Key.R) && divisor.ValidDivisors.Value.Type == BeatDivisorType.Triplets, "R increases type, recorder=" + enabled);
				Check(KeyDown(c.StepRecorder, Key.E) && divisor.ValidDivisors.Value.Type == BeatDivisorType.Common, "E decreases type, recorder=" + enabled);
				Check(Selected().All(n => Samples(n).Count == 3 && Samples(n).All(s => s.Bank == "drum")), "grid keys do not alter selected hitsounds");
			};
		}
		yield return () =>
		{
			c.StepRecorder.Enabled.Value = false;
			realm.Run(r =>
			{
				foreach (var action in new[] { ShredEditorAction.DecreaseGridSpacing, ShredEditorAction.IncreaseGridSpacing, ShredEditorAction.IncreaseGridType, ShredEditorAction.DecreaseGridType }) Check(r.All<RealmKeyBinding>().Count(b => b.RulesetName == "shred" && b.Variant == Ruleset.EDITOR_VARIANT && b.ActionInt == (int)action) == 2, "two persisted settings slots: " + action);
				foreach (var binding in r.All<RealmKeyBinding>().Where(b => b.RulesetName == null && b.Variant == null))
					if (binding.ActionInt == (int)GlobalAction.EditorToggleWhistleSound || binding.ActionInt == (int)GlobalAction.EditorToggleFinishSound || binding.ActionInt == (int)GlobalAction.EditorToggleClapSound) Check(binding.KeyCombination.Equals(new KeyCombination(InputKey.None)), "conflicting native hitsound binding cleared");
			});
			realm.Write(r => r.All<RealmKeyBinding>().Single(b => b.RulesetName == "shred" && b.ActionInt == (int)ShredEditorAction.DecreaseGridSpacing && b.KeyCombinationString == new KeyCombination(InputKey.Q).ToString()).KeyCombination = new KeyCombination(InputKey.T));
		};
		yield return () =>
		{
			divisor.SetArbitraryDivisor(4);
			Check(!KeyDown(c.StepRecorder, Key.Q) && divisor.Value == 4, "rebound secondary no longer responds to Q");
			Check(KeyDown(c.StepRecorder, Key.T) && divisor.Value == 2, "secondary binding responds to custom T");
			Check(KeyDown(c.StepRecorder, Key.I) && divisor.Value == 1, "primary I remains independent");
			map.SelectedHitObjects.Clear();
		};
		yield return () => { Bank("soft").NormalButton.TriggerClick(); Bank("drum").AdditionsButton.TriggerClick(); Toggle(GlobalAction.EditorToggleClapSound); };
		yield return () =>
		{
			var samples = c.CreateRecorderSamples(5000);
			Check(samples.Count == 2 && samples.Single(s => s.Name == HitSampleInfo.HIT_NORMAL).Bank == "soft" && samples.Single(s => s.Name == HitSampleInfo.HIT_CLAP).Bank == "drum", "new recorder notes use current bank and effect choices");
		};
	}
}
