using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Shred.Objects;
namespace osu.Game.Rulesets.Shred.Beatmaps
{
	public class ShredBeatmapConverter : BeatmapConverter<ShredHitObject>
	{
		public ShredBeatmapConverter(IBeatmap beatmap, Ruleset ruleset) : base(beatmap, ruleset) { }
		protected override Beatmap<ShredHitObject> ConvertBeatmap(IBeatmap original, CancellationToken cancellationToken)
		{
			Beatmap<ShredHitObject> converted = base.ConvertBeatmap(original, cancellationToken);
			// The base converter reuses objects already belonging to this ruleset.
            // Each load needs independent objects because mods and gameplay mutate them.
            converted.HitObjects = converted.HitObjects.Select(shred => new ShredHitObject
            {
                StartTime = shred.StartTime, Duration = shred.Duration,
                Row = shred.Row, Fret = shred.Fret, Articulation = shred.Articulation,
                Samples = shred.Samples.Select(sample => sample.With()).ToList()
            }).ToList();
            converted.Difficulty.CircleSize = 12;
			return converted;
		}
		public override bool CanConvert() => Beatmap.HitObjects.All(h => h is ShredHitObject)
			|| (Beatmap.Difficulty.CircleSize == 12 && Beatmap.HitObjects.All(h => h is IHasColumn && !(h is IHasDuration)));
		protected override IEnumerable<ShredHitObject> ConvertHitObject(HitObject original, IBeatmap beatmap, CancellationToken cancellationToken)
		{
			if (original is ShredHitObject shred)
            {
                yield return new ShredHitObject { StartTime = shred.StartTime, Duration = shred.Duration,
                    Row = shred.Row, Fret = shred.Fret, Articulation = shred.Articulation,
                    Samples = shred.Samples.Select(s => s.With()).ToList() };
                yield break;
            }
			if (original is IHasColumn column && column.Column >= 0 && column.Column < 12)
				yield return new ShredHitObject { StartTime = original.StartTime, Samples = original.Samples, Row = column.Column / 6, Fret = column.Column % 6 };
		}
	}
}
