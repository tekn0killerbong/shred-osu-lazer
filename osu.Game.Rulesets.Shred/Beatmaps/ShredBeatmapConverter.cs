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
        private readonly RulesetInfo targetRuleset;
		public ShredBeatmapConverter(IBeatmap beatmap, Ruleset ruleset) : base(beatmap, ruleset) { targetRuleset = ruleset.RulesetInfo; }
		protected override Beatmap<ShredHitObject> ConvertBeatmap(IBeatmap original, CancellationToken cancellationToken)
		{
			Beatmap<ShredHitObject> converted = base.ConvertBeatmap(original, cancellationToken);
			// The base converter reuses objects already belonging to this ruleset.
            // Each load needs independent objects because mods and gameplay mutate them.
            converted.HitObjects = converted.HitObjects.Select(shred => new ShredHitObject
            {
                StartTime = shred.StartTime, Duration = shred.Duration,
                Row = shred.Row, Fret = shred.Fret, Articulation = shred.Articulation,
                Samples = playableSamples(shred)
            }).ToList();
            bool authored = original.HitObjects.All(h => h is ShredHitObject || h is ShredCarrierNote
#if SHRED_OFFICIAL
                || Compatibility.ShredEditorCodec.HasNativeMarker(h)
#endif
            );
            if (authored)
                ShredGridTiming.Repair(converted.HitObjects, converted.ControlPointInfo);
            converted.HitObjects = ShredCoincidentHeads.RemoveDuplicates(converted.HitObjects);
#if SHRED_OFFICIAL
            // EditorBeatmap chooses its processor from the playable map, not from
            // the catalogue identity. Never leave Shred notes labelled standard.
            converted.BeatmapInfo.Ruleset = targetRuleset.Clone();
#endif
            converted.Difficulty.CircleSize = 12;
			return converted;
		}
        private static IList<osu.Game.Audio.HitSampleInfo> playableSamples(ShredHitObject note)
        {
#if SHRED_OFFICIAL
            return Compatibility.ShredEditorCodec.PlayableSamples(note.Samples);
#else
            return note.Samples.Select(s => s.With()).ToList();
#endif
        }
		public override bool CanConvert() => Beatmap.HitObjects.All(h => h is ShredHitObject || h is ShredCarrierNote)
#if SHRED_OFFICIAL
            // Song select requests statistics even while switching from a standard map.
            || Beatmap.HitObjects.All(Compatibility.ShredEditorCodec.HasNativeMarker)
            || Beatmap.HitObjects.All(h => h is IHasPosition)
#endif
			|| (Beatmap.Difficulty.CircleSize == 12 && Beatmap.HitObjects.All(h => h is IHasColumn && !(h is IHasDuration)));
		protected override IEnumerable<ShredHitObject> ConvertHitObject(HitObject original, IBeatmap beatmap, CancellationToken cancellationToken)
		{
#if SHRED_OFFICIAL
            if (Compatibility.ShredEditorCodec.TryDecode(original, out var authored))
            {
                yield return authored;
                yield break;
            }
#endif
			if (original is ShredCarrierNote carrier)
            {
                yield return carrier.ToShred();
                yield break;
            }
			if (original is ShredHitObject shred)
            {
                yield return new ShredHitObject { StartTime = shred.StartTime, Duration = shred.Duration,
                    Row = shred.Row, Fret = shred.Fret, Articulation = shred.Articulation,
                    Samples = playableSamples(shred) };
                yield break;
            }
#if SHRED_OFFICIAL
            if (original is IHasPosition position)
            {
                // Basic standard conversion: horizontal position selects one of five
                // frets, vertical position selects the row. Native carriers exit above.
                yield return new ShredHitObject
                {
                    StartTime = original.StartTime,
                    Row = position.Y < 192 ? 0 : 1,
                    Fret = System.Math.Clamp((int)(position.X * 5 / 512), 0, 4) + 1,
                    Duration = original is IHasDuration duration ? System.Math.Max(0, duration.Duration) : 0,
                    Samples = original.Samples.Select(s => s.With()).ToList()
                };
                yield break;
            }
#endif
			if (original is IHasColumn column && column.Column >= 0 && column.Column < 12)
				yield return new ShredHitObject { StartTime = original.StartTime, Samples = original.Samples, Row = column.Column / 6, Fret = column.Column % 6 };
		}
	}
}
