#if SHRED_OFFICIAL
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using osu.Framework;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Configuration;
using Realms;

namespace osu.Game.Rulesets.Shred.Compatibility
{
    // Observe only plausible native carriers (CS12 is outside standard's decoded
    // range). Verify the actual file before changing any catalogue identity.
    public sealed class ShredAutoImport
    {
        private static readonly ConditionalWeakTable<BeatmapManager, ShredAutoImport> services = new ConditionalWeakTable<BeatmapManager, ShredAutoImport>();
        private readonly HashSet<Guid> queued = new HashSet<Guid>();
        private readonly Dictionary<Guid, string> checkedHashes = new Dictionary<Guid, string>();
        private readonly BeatmapManager manager;
        private readonly RealmAccess realm;
        private readonly osu.Framework.Platform.GameHost game;
        private readonly Bindable<WorkingBeatmap> selected;
        private readonly IDisposable subscription;

        public static void Install(BeatmapManager manager, RealmAccess realm, osu.Framework.Platform.GameHost game, Bindable<WorkingBeatmap> selected)
        {
            game.UpdateThread.Scheduler.Add(() => services.GetValue(manager, _ => new ShredAutoImport(manager, realm, game, selected)));
        }
        private ShredAutoImport(BeatmapManager manager, RealmAccess realm, osu.Framework.Platform.GameHost game, Bindable<WorkingBeatmap> selected)
        {
            this.manager = manager; this.realm = realm; this.game = game; this.selected = selected;
            subscription = realm.RegisterForNotifications(
                r => r.All<BeatmapInfo>().Filter("Ruleset.ShortName == $0 AND Difficulty.CircleSize == 12 AND BeatmapSet.DeletePending == false", "osu"),
                (maps, changes) =>
                {
                    foreach (var map in maps)
                        if (!checkedHashes.TryGetValue(map.ID, out string hash) || hash != map.Hash) enqueue(map.ID);
                });
            // Also repairs older v37 authored maps whose stored CS was clamped to 10.
            selected.BindValueChanged(e =>
            {
                if (e.NewValue?.BeatmapInfo.Ruleset.ShortName == "osu") enqueue(e.NewValue.BeatmapInfo.ID);
            }, true);
        }
        private void enqueue(Guid id)
        {
            if (!queued.Add(id)) return;
            game.UpdateThread.Scheduler.Add(() => inspect(id));
        }
        private void inspect(Guid id)
        {
            queued.Remove(id);
            try
            {
                var info = manager.QueryBeatmap(b => b.ID == id);
                if (info == null || info.Ruleset.ShortName != "osu" || string.IsNullOrEmpty(info.Hash)) return;
                if (checkedHashes.TryGetValue(id, out string hash) && hash == info.Hash) return;
                checkedHashes[id] = info.Hash;
                if (realm.Run(r => IsSuppressed(r, info))) return;
                var working = manager.GetWorkingBeatmap(info, refetch: true);
                if (!IsAuthored(working.Beatmap)) return;
                var selection = ShredNativeIdentity.Validate(info, working.Beatmap);
                realm.Write(r => ShredNativeIdentity.ApplyInTransaction(r, selection, true));
                var updated = manager.QueryBeatmap(b => b.ID == id);
                if (selected.Value.BeatmapInfo.ID == id)
                    selected.Value = manager.GetWorkingBeatmap(updated, refetch: true);
                Logger.Log("Shred: automatically classified an authored difficulty.", LoggingTarget.Runtime, LogLevel.Verbose);
            }
            catch (Exception e)
            {
                Logger.Log($"Shred automatic classification: {e.Message}", LoggingTarget.Runtime, LogLevel.Important);
            }
        }
        public static bool IsAuthored(IBeatmap map)
            => map.HitObjects.Count > 0
                ? map.HitObjects.All(n => n is Beatmaps.ShredCarrierNote)
                : HasTag(map.Metadata.Tags);
        public const string Tag = "shred-native-v1";
        public static bool HasTag(string tags) => (tags ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(Tag);
        private static string key(Guid id) => "KeepStandard_" + id.ToString("N");
        public static bool IsSuppressed(Realm r, BeatmapInfo info)
        {
            string lookup = key(info.ID);
            return r.All<RealmRulesetSetting>().Any(s => s.RulesetName == "shred" && s.Key == lookup && s.Value == info.Hash);
        }
        public static void SetManualChoice(Realm r, BeatmapInfo info, bool native)
        {
            string lookup = key(info.ID);
            foreach (var setting in r.All<RealmRulesetSetting>().Where(s => s.RulesetName == "shred" && s.Key == lookup).ToArray()) r.Remove(setting);
            if (!native) r.Add(new RealmRulesetSetting { RulesetName = "shred", Variant = 0, Key = lookup, Value = info.Hash });
        }
    }
}
#endif
