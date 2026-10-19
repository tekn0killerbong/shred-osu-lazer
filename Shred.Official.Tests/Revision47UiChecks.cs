using System.Diagnostics;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components.TernaryButtons;
using osu.Game.Skinning;

internal static class Revision47UiChecks
{
	public static IEnumerable<Action> Steps(Editor editor)
	{
		var c = editor.ChildrenOfType<ShredHitObjectComposer>().Single();
		var map = c.Dependencies.Get<EditorBeatmap>(); var clock = c.Dependencies.Get<EditorClock>();
		int updates = 0;
		map.BeatmapReprocessed += () => updates++;
		if (EditorUiProbe.TestSkinArchive != null)
		{
			var skins = c.Dependencies.Get<SkinManager>();
			var known = skins.GetAllUsableSkins().Select(s=>s.PerformRead(i=>i.ID)).ToHashSet();
			var path = EditorUiProbe.TestSkinArchive;
			var import = skins.Import(new[] { new ImportTask(File.OpenRead(path), Path.GetFileName(path)) });
			while (!import.IsCompleted) yield return () => {};
			// Import completion precedes the UI thread's Realm notification under load.
			import.GetAwaiter().GetResult();
			while (!skins.GetAllUsableSkins().Any(s=>s.PerformRead(i=>!known.Contains(i.ID)))) yield return () => {};
			yield return () => { import.GetAwaiter().GetResult(); skins.CurrentSkinInfo.Value=skins.GetAllUsableSkins().Single(s=>s.PerformRead(i=>!known.Contains(i.ID))); };
			yield return () => {};
		}
		void Dump(string label)
		{
			Console.WriteLine($"DIAG {label} notes={map.HitObjects.Count} reprocess={updates}");
			foreach (var d in c.ChildrenOfType<DrawableShredEditorNote>().Take(3))
			{
				Console.WriteLine($"NOTE {d.HitObject.StartTime} {string.Join(';', d.GetSamples().Select(s => $"{s.GetType().Name}:{string.Join(',',s.LookupNames)}:{s.Volume}"))}");
				foreach (var p in d.ChildrenOfType<PoolableSkinnableSample>())
				{
					var info = (ISampleInfo)typeof(PoolableSkinnableSample).GetField("sampleInfo", BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(p)!;
					Console.WriteLine($"POOL {string.Join(',', info.LookupNames)} played={p.Played} volume={p.AggregateVolume.Value} frequency={p.AggregateFrequency.Value}");
					if (p.Sample == null) { Console.WriteLine("SAMPLE null"); continue; }
					foreach (var f in p.Sample.GetType().GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
						if (f.GetValue(p.Sample) is ISample s) Console.WriteLine($"SAMPLE {f.Name} {s.Name} length={s.Length} loaded={s.IsLoaded} type={s.GetType().Name}");
				}
			}
		}
		long allocated = 0; var watch = new Stopwatch(); double startFrame = 0;
		yield return () => { clock.Stop(); c.Playback.ReturnOnPause.Value = false; clock.Seek(0); Dump("opened"); };
		yield return () => { allocated = GC.GetTotalAllocatedBytes(); watch.Start(); startFrame = editor.Clock.CurrentTime; EditorUiProbe.FrameCosts.Clear(); EditorUiProbe.CaptureFrames=true; };
		for (int i=0;i<8;i++) yield return () => {};
		yield return () => { EditorUiProbe.CaptureFrames=false; var frames=EditorUiProbe.FrameCosts.Order().ToArray(); Console.WriteLine($"IDLE frames={frames.Length} median={frames[frames.Length/2]:F3} p95={frames[(int)(frames.Length*0.95)]:F3} elapsed={watch.ElapsedMilliseconds} allocated={GC.GetTotalAllocatedBytes()-allocated} clockDelta={editor.Clock.CurrentTime-startFrame}"); Dump("idle"); map.SelectedHitObjects.AddRange(map.HitObjects); };
		yield return () =>
		{
			var first = map.HitObjects.First(); int changes=0;
			System.Collections.Specialized.NotifyCollectionChangedEventHandler observe=(_,_)=>changes++;
			first.SamplesBindable.CollectionChanged+=observe;
			c.ChildrenOfType<SampleBankTernaryButton>().Single(b=>b.BankName=="soft").NormalButton.TriggerClick();
			first.SamplesBindable.CollectionChanged-=observe;
			Revision47Checks.Check(changes==2, "bank click publishes one final sample list (clear/add), without intermediate audio reloads");
		};
		yield return () => { Dump("soft"); clock.Start(); };
		while (clock.CurrentTime < 1600) yield return () => {};
		yield return () => { clock.Stop(); Dump("played soft"); c.ChildrenOfType<SampleBankTernaryButton>().Single(b=>b.BankName=="drum").NormalButton.TriggerClick(); };
		yield return () => { Dump("drum"); clock.Seek(0); };
		yield return () => { clock.Start(); };
		while (clock.CurrentTime < 1600) yield return () => {};
		yield return () => { clock.Stop(); Dump("played drum"); };
		foreach (string bank in HitSampleInfo.ALL_BANKS)
		{
			yield return () =>
			{
				c.ChildrenOfType<SampleBankTernaryButton>().Single(b=>b.BankName==bank).NormalButton.TriggerClick();
				c.ChildrenOfType<SampleBankTernaryButton>().Single(b=>b.BankName==bank).AdditionsButton.TriggerClick();
				foreach (var pair in new[] { (HitSampleInfo.HIT_WHISTLE, GlobalAction.EditorToggleWhistleSound), (HitSampleInfo.HIT_FINISH, GlobalAction.EditorToggleFinishSound), (HitSampleInfo.HIT_CLAP, GlobalAction.EditorToggleClapSound) })
					if(c.SelectionSampleStates[pair.Item1].Value != TernaryState.True)
						c.ChildrenOfType<DrawableTernaryButton<GlobalAction>>().Single(b=>b.Action==pair.Item2).TriggerClick();
				clock.Seek(0);
			};
			yield return () =>
			{
				foreach (var d in c.ChildrenOfType<DrawableShredEditorNote>())
				{
					var channels=d.ChildrenOfType<PoolableSkinnableSample>().ToArray();
					if(channels.Length!=4) throw new Exception("Extra or missing sample set on note "+d.HitObject.StartTime);
					foreach (var p in channels)
					{
						var info=(HitSampleInfo)typeof(PoolableSkinnableSample).GetField("sampleInfo",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(p)!;
						var sample=(ISample)p.Sample.GetType().GetField("sample",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(p.Sample)!;
						string name=bank+"-"+info.Name;
						double expected=Revision45Checks.Files.Single(f=>f.name==name).ms;
						if(sample.Name!="Gameplay/"+name || !sample.IsLoaded || Math.Abs(sample.Length-expected)>2 || p.AggregateFrequency.Value!=1
							|| Math.Abs(p.AggregateVolume.Value-info.Volume/100.0)>0.000001)
							throw new Exception($"Wrong audible sample: {name}, actual {sample.Name}/{sample.Length} ms, frequency {p.AggregateFrequency.Value}");
					}
				}
				Console.WriteLine("PASS v47 every note loads all four original WAVs for bank "+bank);
				clock.Start();
			};
			while(clock.CurrentTime<1600) yield return ()=>{};
			yield return () =>
			{
				clock.Stop();
				foreach(var d in c.ChildrenOfType<DrawableShredEditorNote>().Where(d=>d.HitObject.StartTime<1600))
					Revision47Checks.Check(d.ChildrenOfType<PoolableSkinnableSample>().Count(p=>p.Played)==4, "all selected effects actually play for "+bank);
			};
		}
	}
}
