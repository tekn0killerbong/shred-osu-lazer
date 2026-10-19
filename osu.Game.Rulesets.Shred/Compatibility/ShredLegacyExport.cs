#if SHRED_OFFICIAL
using System;
using System.Reflection;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Shred.Objects;

namespace osu.Game.Rulesets.Shred.Compatibility
{
    // The official manager does not expose an exporter registration API. Replace
    // only its stock exporter, retaining the stock UI, archive handling, naming,
    // notifications and standard-ruleset path. Never alter catalogue identities.
    public sealed class ShredLegacyExport : LegacyBeatmapExporter
    {
        public ShredLegacyExport(Storage storage) : base(storage) { }

        public static void Install(BeatmapManager manager, Storage storage)
        {
            var field = typeof(BeatmapManager).GetField("legacyBeatmapExporter", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(manager) is not LegacyBeatmapExporter current || current.GetType() != typeof(LegacyBeatmapExporter))
                return;
            try
            {
                field.SetValue(manager, new ShredLegacyExport(storage) { PostNotification = current.PostNotification });
            }
            catch (Exception e)
            {
                Logger.Log($"Shred export integration unavailable: {e.Message}", LoggingTarget.Runtime, LogLevel.Important);
            }
        }

        protected override void MutateBeatmap(BeatmapSetInfo beatmapSet, IBeatmap playableBeatmap)
        {
            if (playableBeatmap.BeatmapInfo.Ruleset.ShortName != "shred")
            {
                base.MutateBeatmap(beatmapSet, playableBeatmap);
                return;
            }

            // Use the editor's lossless carrier on this export copy only. The
            // stock legacy mutation rounds X, which is Shred's logical column,
            // and truncates time: neither operation belongs on Shred objects.
            playableBeatmap.BeatmapInfo = playableBeatmap.BeatmapInfo.Clone();
            playableBeatmap.BeatmapInfo.Ruleset = playableBeatmap.BeatmapInfo.Ruleset.Clone();
            playableBeatmap.BeatmapInfo.Ruleset.OnlineID = 3;
            playableBeatmap.BeatmapInfo.Metadata = playableBeatmap.BeatmapInfo.Metadata.DeepClone();
            if (!ShredAutoImport.HasTag(playableBeatmap.Metadata.Tags))
                playableBeatmap.Metadata.Tags = (playableBeatmap.Metadata.Tags + " " + ShredAutoImport.Tag).Trim();
            foreach (ShredHitObject note in playableBeatmap.HitObjects)
                ShredEditorCodec.Synchronise(note);
        }
    }
}
#endif
