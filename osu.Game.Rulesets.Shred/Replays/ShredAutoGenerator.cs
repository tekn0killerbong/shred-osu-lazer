using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Shred.Gameplay;

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
			var random = new Random(0x534852);
            var chords = new List<List<ShredHitObject>>();
            foreach (var row in Beatmap.HitObjects.OfType<ShredHitObject>().Where(n => !n.IsWham).GroupBy(n => n.Row))
            {
                List<ShredHitObject> current = null;
                foreach (var note in row.OrderBy(n => n.StartTime))
                {
                    // Match gameplay's tolerance for legacy integer timestamps
                    // beside editor-created fractional timestamps.
                    if (current == null || !ShredChordRules.SameChordTime(current[0].StartTime, note.StartTime))
                    {
                        current = new List<ShredHitObject>();
                        chords.Add(current);
                    }
                    current.Add(note);
                }
            }
            foreach (var chord in chords.OrderBy(c => c[0].StartTime).ThenBy(c => c[0].Row))
			{
				double startTime = chord[0].StartTime;
				int row = chord[0].Row;
				bool needsPick = chord.Any(n => n.Articulation == ShredPracticeArticulation.Pick);
                if (needsPick)
                {
                    int count = ShredChordRules.PickCount(chord.Where(n => n.Articulation == ShredPracticeArticulation.Pick)
                        .Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.Fret)));
                    int[] order = Enumerable.Range(0, 4).OrderBy(_ => random.Next()).ToArray();
                    for (int i = 0; i < count; i++)
                    {
                        ShredAction pick = (ShredAction)(105 + row * 9 + order[i % 4]);
                        change(startTime + i * 0.002, pick, 1);
                        change(startTime + i * 0.002 + 0.001, pick, -1);
                    }
                }
                else if (chord.All(n => n.Fret == 0))
                {
                    // An open HP requires a real release edge, never an idle open row.
                    ShredAction fret = (ShredAction)(100 + row * 9);
                    change(startTime - 0.02, fret, 1);
                    change(startTime, fret, -1);
                }
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
