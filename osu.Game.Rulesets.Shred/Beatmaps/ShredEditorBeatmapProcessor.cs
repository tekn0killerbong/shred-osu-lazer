using System.Runtime.CompilerServices;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Shred.Objects;

namespace osu.Game.Rulesets.Shred.Beatmaps
{
	public sealed class ShredEditorBeatmapProcessor : BeatmapProcessor
	{
		private sealed class Position
		{
			public double Start;
			public double Duration;
		}
		private readonly ConditionalWeakTable<ShredHitObject, Position> processed = new();
		public ShredEditorBeatmapProcessor(IBeatmap beatmap) : base(beatmap) { }
		public override void PreProcess()
		{
			var changed = new List<ShredHitObject>();
			foreach (ShredHitObject note in Beatmap.HitObjects)
			{
				if (!processed.TryGetValue(note, out var position))
				{
					position = new Position { Start = double.NaN };
					processed.Add(note, position);
				}
				// Do not repeatedly scan unchanged notes, or move them when a timing
				// point alone is edited with "Move already placed objects" disabled.
				if (position.Start != note.StartTime || position.Duration != note.Duration)
					changed.Add(note);
			}
			ShredGridTiming.Repair(changed, Beatmap.ControlPointInfo);
			foreach (ShredHitObject note in Beatmap.HitObjects)
			{
				var position = processed.GetValue(note, _ => new Position());
				position.Start = note.StartTime;
				position.Duration = note.Duration;
#if SHRED_OFFICIAL
				Compatibility.ShredEditorCodec.Synchronise(note);
#endif
			}
			base.PreProcess();
		}
	}
}
