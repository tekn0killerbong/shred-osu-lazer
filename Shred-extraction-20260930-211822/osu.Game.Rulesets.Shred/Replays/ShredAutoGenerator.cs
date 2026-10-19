using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.Shred.Replays
{
	public class ShredAutoGenerator : AutoGenerator<ShredReplayFrame>
	{
		public ShredAutoGenerator(IBeatmap beatmap) : base(beatmap) { }
		protected override void GenerateFrames()
		{
			var events = new SortedDictionary<double, List<(ShredAction action, int change)>>();
			void change(double time, ShredAction action, int amount)
			{
				if (!events.TryGetValue(time, out var list)) events[time] = list = new List<(ShredAction, int)>();
				list.Add((action, amount));
			}
			foreach (ShredHitObject note in Beatmap.HitObjects.OfType<ShredHitObject>())
			{
				if (note.IsWham)
				{
					change(note.StartTime, ShredAction.Wham, 1);
					change(Math.Max(note.StartTime + 0.01, note.EndTime - 0.01), ShredAction.Wham, -1);
					continue;
				}
				if (note.Fret > 0)
				{
					ShredAction fret = (ShredAction)(100 + note.Row * 9 + note.Fret - 1);
					change(note.StartTime - 0.02, fret, 1);
					double release = note.StartTime + 0.02;
					if (note.IsLong) release = Math.Max(note.StartTime + 0.01, note.EndTime - 0.01);
					change(release, fret, -1);
				}
			}
			foreach (var chord in Beatmap.HitObjects.OfType<ShredHitObject>().Where(n => !n.IsWham).GroupBy(n => (n.StartTime, n.Row)))
			{
				ShredAction pick = (ShredAction)(105 + chord.Key.Row * 9);
				change(chord.Key.StartTime, pick, 1);
				change(chord.Key.StartTime + 0.01, pick, -1);
			}
			var held = new Dictionary<ShredAction, int>();
			Frames.Add(new ShredReplayFrame { Time = events.Keys.DefaultIfEmpty(0).Min() - 1 });
			foreach (var item in events)
			{
				foreach (var transition in item.Value)
				{
					held.TryGetValue(transition.action, out int count);
					held[transition.action] = count + transition.change;
				}
				Frames.Add(new ShredReplayFrame { Time = item.Key, Actions = held.Where(p => p.Value > 0).Select(p => p.Key).OrderBy(a => a).ToList() });
			}
		}
	}
}
