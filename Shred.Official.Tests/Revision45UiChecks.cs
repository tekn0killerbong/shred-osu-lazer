using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Skinning;
using osuTK.Input;

internal static class Revision45UiChecks
{
	private static void KeyDown(ShredStepRecorder recorder, Key key)
	{
		var state = new InputState(); state.Keyboard.Keys.SetPressed(key, true);
		typeof(ShredStepRecorder).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!
			.Invoke(recorder, new object[] { new KeyDownEvent(state, key) });
	}
	public static IEnumerable<Action> Steps(Editor editor)
	{
		var c = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
		var divisor = c.Dependencies.Get<BindableBeatDivisor>();
		var skins = c.Dependencies.Get<SkinManager>();
		var provider = c.Dependencies.Get<ISkinSource>();
		var originalSkin = skins.CurrentSkinInfo.Value;
		foreach (var skin in skins.GetAllUsableSkins().Where(s => s.PerformRead(i => i.Protected)).ToArray())
		{
			var samples = new List<(ISample sample, double ms)>();
			yield return () => { skins.CurrentSkinInfo.Value = skin; };
			yield return () =>
			{
				foreach (var file in Revision45Checks.Files)
				{
					var parts = file.name.Split('-');
					var info = new HitSampleInfo(parts[1], parts[0]);
					var sample = provider.GetSample(info);
					Revision45Checks.Check(sample != null, $"editor resolves {file.name} with {skins.CurrentSkin.Value.Name}");
					samples.Add((sample, file.ms));
					sample.GetChannel().Play();
				}
			};
			yield return () =>
			{
				foreach (var entry in samples)
				{
					Console.WriteLine($"AUDIO {entry.sample.Name} {entry.sample.GetType().Name} loaded={entry.sample.IsLoaded} length={entry.sample.Length} expected={entry.ms}");
					Revision45Checks.Check(entry.sample.IsLoaded && Math.Abs(entry.sample.Length - entry.ms) < 2,
						"editor decoded supplied audio duration: " + entry.sample.Name);
				}
			};
		}
		yield return () =>
		{
			skins.CurrentSkinInfo.Value = originalSkin;
			c.StepRecorder.Enabled.Value = false; divisor.SetArbitraryDivisor(4);
			KeyDown(c.StepRecorder, Key.I);
			Revision45Checks.Check(divisor.Value == 2, "I increases spacing: 1/4 to 1/2");
			KeyDown(c.StepRecorder, Key.O);
			Revision45Checks.Check(divisor.Value == 4, "O decreases spacing: 1/2 to 1/4");
			KeyDown(c.StepRecorder, Key.P);
			Revision45Checks.Check(divisor.ValidDivisors.Value.Type == BeatDivisorType.Triplets, "P increases type: Common to Triplets");
			KeyDown(c.StepRecorder, Key.BracketLeft);
			Revision45Checks.Check(divisor.ValidDivisors.Value.Type == BeatDivisorType.Common, "circumflex physical key decreases type: Triplets to Common");
		};
		var map = c.Dependencies.Get<EditorBeatmap>();
		var clock = c.Dependencies.Get<EditorClock>();
		ShredHitObject tap = null, hold = null, wham = null;
		PoolableSkinnableSample holdSound = null;
		object holdChannel = null;
		bool Played(ShredHitObject note) => c.ChildrenOfType<DrawableShredEditorNote>().Single(n => n.HitObject == note)
			.ChildrenOfType<PoolableSkinnableSample>().Any(s => s.Played);
		yield return () =>
		{
			clock.Stop(); c.Playback.ReturnOnPause.Value = false; map.BeginChange();
			foreach (var n in map.HitObjects.ToArray()) map.Remove(n);
			tap = new ShredHitObject { StartTime = 1000, Fret = 1, Samples = new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) } };
			hold = new ShredHitObject { StartTime = 1250, Fret = 2, Duration = 750, Samples = new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT) } };
			wham = new ShredHitObject { StartTime = 1500, Articulation = ShredPracticeArticulation.Wham, Duration = 500, Samples = new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_FINISH, HitSampleInfo.BANK_DRUM) } };
			map.Add(tap); map.Add(hold); map.Add(wham); map.EndChange(); clock.Seek(0);
		};
		yield return () => { clock.Seek(2500); };
		yield return () =>
		{
			Revision45Checks.Check(!Played(tap) && !Played(hold) && !Played(wham), "paused forward seek does not audition crossed notes");
			clock.Seek(500);
		};
		yield return () => { clock.Start(); };
		while (clock.CurrentTime < 1700) yield return () => { };
		yield return () =>
		{
			Revision45Checks.Check(Played(tap) && Played(hold) && Played(wham), "editor playback auditions short note, LN head and wham head");
			var drawable = c.ChildrenOfType<DrawableShredEditorNote>().Single(n => n.HitObject == hold);
			holdSound = drawable.ChildrenOfType<PoolableSkinnableSample>().First(s => s.Played);
			holdChannel = typeof(PoolableSkinnableSample).GetField("activeChannel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(holdSound);
			Revision45Checks.Check(!drawable.Judged, "audition leaves editor objects unjudged");
		};
		while (clock.CurrentTime < 2300) yield return () => { };
		yield return () =>
		{
			Revision45Checks.Check(ReferenceEquals(holdChannel, typeof(PoolableSkinnableSample).GetField("activeChannel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(holdSound)), "LN tail does not replay head sound");
			clock.Stop();
		};

	}
}
