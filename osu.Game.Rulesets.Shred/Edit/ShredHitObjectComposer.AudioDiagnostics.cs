using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Audio;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Shred.Edit
{
    public partial class ShredHitObjectComposer
    {
        private SkinManager diagnosticSkins;
        private ScheduledDelegate pendingAudioDiagnostic;
        private int audioDiagnosticCount;

        [BackgroundDependencyLoader]
        private void loadAudioDiagnostics(SkinManager skins)
        {
            diagnosticSkins = skins;
        }

        // Temporary, bounded diagnostics for the Windows-only report. Never run
        // in the frame loop, write per-note logs, or play sounds for the diagnostic.
        private void queueAudioDiagnostic(string reason)
        {
            if (audioDiagnosticCount >= 16) return;
            pendingAudioDiagnostic?.Cancel();
            pendingAudioDiagnostic = Scheduler.AddDelayed(() => reportAudioDiagnostic(reason), 300);
        }

        private void reportAudioDiagnostic(string reason)
        {
            audioDiagnosticCount++;
            try
            {
                var representative = Grid.AllHitObjects.OfType<DrawableShredEditorNote>()
                    .FirstOrDefault(d => EditorBeatmap.SelectedHitObjects.Contains(d.HitObject))
                    ?? Grid.AllHitObjects.OfType<DrawableShredEditorNote>().FirstOrDefault();
                if (representative == null) return;
                var source = representative.Dependencies.Get<ISkinSource>();
                var assembly = typeof(ShredRuleset).Assembly;
                string skinName = diagnosticSkins.CurrentSkin.Value.SkinInfo.PerformRead(info => info.Name);
#if SHRED_OFFICIAL
                const string build = "official";
#else
                const string build = "preview";
#endif
                logAudio($"{reason}; build={build}; module={assembly.ManifestModule.ModuleVersionId}; skin={skinName}; selected={EditorBeatmap.SelectedHitObjects.Count}; notes={EditorBeatmap.HitObjects.Count}");
                logAudio("sources=" + string.Join(",", source.AllSources.Select(s => s.GetType().FullName)));

                var samples = representative.GetSamples().ToArray();
                logAudio("note=" + string.Join(";", samples.Select(s => $"{s.GetType().Name} bank={s.Bank} name={s.Name} volume={s.Volume} beatmap={s.UseBeatmapSamples}")));
                foreach (var sample in samples)
                    logAudio($"note lookup {string.Join(",", sample.LookupNames)} => {describeSample(source, sample)}");

                // Report all twelve defaults once, then just the edited note.
                if (audioDiagnosticCount == 1)
                    foreach (string bank in HitSampleInfo.ALL_BANKS)
                    foreach (string name in HitSampleInfo.ALL_ADDITIONS.Prepend(HitSampleInfo.HIT_NORMAL))
                        logAudio($"default {bank}-{name} => {describeSample(source, new HitSampleInfo(name, bank))}");
            }
            catch (Exception ex)
            {
                // A failed diagnostic must not interrupt the editor.
                logAudio("diagnostic unavailable: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static string describeSample(ISkinSource source, ISampleInfo info)
        {
            var sample = source.GetSample(info);
            if (sample == null) return "MISSING";
            return FormattableString.Invariant($"{sample.Name}; loaded={sample.IsLoaded}; duration={sample.Length:F1}ms");
        }

        private static void logAudio(string message)
            => Logger.Log("[shred v49 audio] " + message, LoggingTarget.Runtime, LogLevel.Verbose);
    }
}
