#if SHRED_OFFICIAL
using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Shred.Compatibility
{
    public static class ShredLegacyEditorBridge
    {
        public static IBeatmapProcessor CreateProcessor(IBeatmap beatmap)
        {
            if (beatmap is not EditorBeatmap editor) return null;
            if (editor.BeatmapInfo.Ruleset.ShortName != "shred" || editor.BeatmapInfo.IsManaged)
                throw new InvalidOperationException("L'adaptation Shred exige une carte d'éditeur détachée.");
            // Only this detached editor identity uses mode 3 to retain scroll-speed effects. The installed ruleset
            // and database identity stay custom, including while the editor is open.
            if (!ShredAutoImport.HasTag(editor.Metadata.Tags))
                editor.Metadata.Tags = (editor.Metadata.Tags + " " + ShredAutoImport.Tag).Trim();
            var identity = editor.BeatmapInfo.Ruleset.Clone();
            identity.OnlineID = 3;
            editor.BeatmapInfo.Ruleset = identity;
            var processor = new Beatmaps.ShredEditorBeatmapProcessor(editor);
            processor.PreProcess();
            return processor;
        }

    }
}
#endif
