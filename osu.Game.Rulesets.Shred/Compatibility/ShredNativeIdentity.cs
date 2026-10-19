#if SHRED_OFFICIAL
using System;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Shred.Beatmaps;
using Realms;

namespace osu.Game.Rulesets.Shred.Compatibility
{
    // The file remains the native Shred export. Only its local catalogue identity changes.
    public static class ShredNativeIdentity
    {
        public sealed class Selection
        {
            public Guid ID { get; }
            public string Hash { get; }
            public string OriginalRuleset { get; }
            internal Selection(BeatmapInfo info) { ID = info.ID; Hash = info.Hash; OriginalRuleset = info.Ruleset.ShortName; }
        }

        public static Selection Validate(BeatmapInfo info, IBeatmap decoded)
        {
            if (info.Ruleset.ShortName != "osu" && info.Ruleset.ShortName != "shred")
                throw new InvalidOperationException("Select an authored Shred map classified as standard or Shred.");
            if (string.IsNullOrEmpty(info.Hash) || !ShredAutoImport.IsAuthored(decoded))
                throw new InvalidOperationException("This map does not contain verifiable native Shred data.");
            var converter = new ShredRuleset().CreateBeatmapConverter(decoded);
            if (!converter.CanConvert() || converter.Convert().HitObjects.Count != decoded.HitObjects.Count)
                throw new InvalidOperationException("The Shred data could not be read completely.");
            return new Selection(info);
        }

        // Called inside RealmAccess.Write. No file, score, collection, or global ruleset is rewritten.
        public static void ApplyInTransaction(Realm realm, Selection selection, bool native)
        {
            if (!realm.IsInTransaction) throw new InvalidOperationException("A transaction is required.");
            var live = realm.Find<BeatmapInfo>(selection.ID) ?? throw new InvalidOperationException("Beatmap not found.");
            if (live.Hash != selection.Hash || live.Ruleset.ShortName != selection.OriginalRuleset)
                throw new InvalidOperationException("The beatmap changed. Select it again.");
            string targetName = native ? "shred" : "osu";
            var target = realm.Find<RulesetInfo>(targetName);
            if (target == null || !target.Available || target.OnlineID != (native ? -1 : 0))
                throw new InvalidOperationException("The target ruleset is unavailable.");
            if (live.Ruleset.ShortName == targetName) return;
            live.Ruleset = target;
            live.StarRating = -1;
        }
    }
}
#endif
