#if SHRED_OFFICIAL
using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;

namespace osu.Game.Rulesets.Shred.Compatibility
{
    public partial class ShredNativeSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Shred beatmaps";
        [BackgroundDependencyLoader]
        private void load(osu.Framework.Platform.Storage storage, BeatmapManager manager, RealmAccess realm, Bindable<WorkingBeatmap> beatmap, INotificationOverlay notifications, osu.Framework.Platform.GameHost game)
        {
            ShredLegacyExport.Install(manager, storage);
            ShredAutoImport.Install(manager, realm, game, beatmap);
            Add(new SettingsButtonV2
            {
                Text = "Classify selected beatmap as Shred",
                Action = () => change(true)
            });
            Add(new SettingsButtonV2
            {
                Text = "Restore osu!standard classification",
                Action = () => change(false)
            });
            Add(new SettingsButtonV2
            {
                Text = "Create an empty Shred difficulty for this song",
                Action = () =>
                {
                    try
                    {
                        var selected = manager.QueryBeatmap(b => b.ID == beatmap.Value.BeatmapInfo.ID)
                            ?? throw new InvalidOperationException("Select an imported song.");
                        var reference = manager.GetWorkingBeatmap(selected, refetch: true);
                        var standard = realm.Run(r => r.Find<RulesetInfo>("osu")?.Detach())
                            ?? throw new InvalidOperationException("The osu!standard ruleset could not be found.");
                        // Create a separate empty difficulty via the stock mode-0 writer;
                        // leave the selected source's notes and identity untouched.
                        var created = manager.CreateNewDifficulty(selected.BeatmapSet, reference, standard);
                        realm.Write(r =>
                        {
                            var native = r.Find<RulesetInfo>("shred")
                                ?? throw new InvalidOperationException("The Shred ruleset could not be found.");
                            var live = r.Find<BeatmapInfo>(created.BeatmapInfo.ID)
                                ?? throw new InvalidOperationException("The new difficulty could not be found.");
                            live.Ruleset = native;
                            live.StarRating = -1;
                        });
                        var updated = manager.QueryBeatmap(b => b.ID == created.BeatmapInfo.ID);
                        beatmap.Value = manager.GetWorkingBeatmap(updated, refetch: true);
                        notifications.Post(new SimpleNotification { Text = "Empty Shred difficulty created. Open Edit to start mapping." });
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Shred editor creation: {ex}", LoggingTarget.Runtime, LogLevel.Important);
                        notifications.Post(new SimpleNotification { Text = "Shred: " + ex.Message });
                    }
                }
            });
            void change(bool native)
            {
                try
                {
                    var selected = beatmap.Value;
                    var info = manager.QueryBeatmap(b => b.ID == selected.BeatmapInfo.ID)
                        ?? throw new InvalidOperationException("Select an imported beatmap in song select.");
                    var fresh = manager.GetWorkingBeatmap(info, refetch: true);
                    var selection = ShredNativeIdentity.Validate(fresh.BeatmapInfo, fresh.Beatmap);
                    realm.Write(r =>
                    {
                        ShredNativeIdentity.ApplyInTransaction(r, selection, native);
                        ShredAutoImport.SetManualChoice(r, info, native);
                    });
                    var updated = manager.QueryBeatmap(b => b.ID == selection.ID)
                        ?? throw new InvalidOperationException("Classification saved; reopen song select.");
                    beatmap.Value = manager.GetWorkingBeatmap(updated, refetch: true);
                    notifications.Post(new SimpleNotification { Text = native
                        ? "Beatmap classified as Shred. Select Shred to play."
                        : "osu!standard classification restored." });
                }
                catch (Exception ex)
                {
                    Logger.Log($"Shred native identity: {ex}", LoggingTarget.Runtime, LogLevel.Important);
                    notifications.Post(new SimpleNotification { Text = "Shred: " + ex.Message });
                }
            }
        }
    }
}
#endif
