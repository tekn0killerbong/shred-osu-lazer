// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Utils;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred.Beatmaps;
using osu.Game.Rulesets.Shred.Mods;
using osu.Game.Rulesets.Shred.UI;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Shred
{
    public partial class ShredRuleset : Ruleset, ILegacyBeatmapExtension
    {
        public override osu.Game.Rulesets.Edit.HitObjectComposer CreateHitObjectComposer() => new Edit.ShredHitObjectComposer(this);

        public override IEnumerable<Drawable> CreateEditorSetupSections() => new Drawable[]
        {
            new osu.Game.Screens.Edit.Setup.MetadataSection(),
            new Edit.ShredDifficultySection(),
            new osu.Game.Screens.Edit.Setup.ResourcesSection()
        };

        public override string Description => "osu!shred";

        public override osu.Game.Overlays.Settings.RulesetSettingsSubsection CreateSettings() => new Edit.ShredEditorSettings(this);

        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod> mods = null) => new DrawableShredRuleset(this, beatmap, mods);

        public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) => new ShredBeatmapConverter(beatmap, this);

        public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new ShredDifficultyCalculator(RulesetInfo, beatmap);

        public override IEnumerable<Mod> GetModsFor(ModType type)
        {
            switch (type)
            {
                case ModType.DifficultyReduction:
                    return new Mod[] { new ShredModNoFail(), new ShredModHalfTime() };

                case ModType.DifficultyIncrease:
                    return new Mod[] { new ShredModDoubleTime(), new ShredModSuddenDeath(), new ShredModHidden() };
                case ModType.Conversion:
                    return new Mod[] { new ShredModGuitarHero(), new ShredModMirror() };
                case ModType.Automation:
                    return new Mod[] { new ShredModAutoplay() };

                default:
                    return Array.Empty<Mod>();
            }
        }

        public override IEnumerable<RulesetBeatmapAttribute> GetBeatmapAttributesForDisplay(IBeatmapInfo info, IReadOnlyCollection<Mod> mods)
        {
            var adjusted = GetAdjustedDisplayDifficulty(info, mods);
            var windows = new Scoring.ShredHitWindows();
            windows.SetDifficulty(adjusted.OverallDifficulty);
            double rate = ModUtils.CalculateRateWithMods(mods);
            var colours = new OsuColour();
            yield return new RulesetBeatmapAttribute(osu.Game.Localisation.SongSelectStrings.Accuracy, "OD", info.Difficulty.OverallDifficulty, adjusted.OverallDifficulty, 10)
            {
                Description = "Affects timing requirements for notes. Early Miss extends 150 ms before Great (scaled by playback rate). Unhit notes expire immediately after late Great; there is no extra late Miss window. Empty picks outside all windows have no penalty. Picked hammer-pulls receive Good within Great. Long-note early release tolerance: 350 ms.",
                AdditionalMetrics = new[] {
                    new RulesetBeatmapAttribute.AdditionalMetric("GREAT", $"±{windows.WindowFor(HitResult.Great) / rate:0.##} ms", colours.ForHitResult(HitResult.Great)),
                    new RulesetBeatmapAttribute.AdditionalMetric("EARLY MISS", $"−{windows.WindowFor(HitResult.Miss) / rate:0.##} to −{windows.WindowFor(HitResult.Great) / rate:0.##} ms", colours.ForHitResult(HitResult.Miss)),
                    new RulesetBeatmapAttribute.AdditionalMetric("LATE MISS", $"> +{windows.WindowFor(HitResult.Great) / rate:0.##} ms", colours.ForHitResult(HitResult.Miss))
                }
            };
            yield return new RulesetBeatmapAttribute(osu.Game.Localisation.SongSelectStrings.HPDrain, "HP", info.Difficulty.DrainRate, adjusted.DrainRate, 10);
        }

        public override string ShortName => "shred";

        public override IEnumerable<HitResult> GetValidHitResults() => new[]
        {
            HitResult.Great, HitResult.Good, HitResult.Miss
        };


        public override IEnumerable<KeyBinding> GetDefaultKeyBindings(int variant = 0)
        {
            if (variant == EDITOR_VARIANT)
                return new[]
                {
                    new KeyBinding(InputKey.Number2, Edit.ShredEditorAction.Pick),
                    new KeyBinding(InputKey.Number3, Edit.ShredEditorAction.HammerPull),
                    new KeyBinding(InputKey.J, Edit.ShredEditorAction.EmptyStep),
                    new KeyBinding(InputKey.N, Edit.ShredEditorAction.EmptyStepAlternate),
                    new KeyBinding(InputKey.I, Edit.ShredEditorAction.DecreaseGridSpacing),
                    new KeyBinding(InputKey.O, Edit.ShredEditorAction.IncreaseGridSpacing),
                    new KeyBinding(InputKey.P, Edit.ShredEditorAction.DecreaseGridType),
                    new KeyBinding(Input.ShredDefaultKeys.ModeIncrease, Edit.ShredEditorAction.IncreaseGridType),
                    new KeyBinding(new[] { InputKey.Control, InputKey.Q }, Edit.ShredEditorAction.ToggleStepRecorder)
                };
            List<KeyBinding> bindings = new List<KeyBinding>
        {
            new KeyBinding(InputKey.A, ShredAction.Row1Fret1),
            new KeyBinding(InputKey.S, ShredAction.Row1Fret2),
            new KeyBinding(InputKey.D, ShredAction.Row1Fret3),
            new KeyBinding(InputKey.F, ShredAction.Row1Fret4),
            new KeyBinding(InputKey.G, ShredAction.Row1Fret5),
            new KeyBinding(InputKey.K, ShredAction.Row1Pick1),
            new KeyBinding(InputKey.L, ShredAction.Row1Pick2),
            new KeyBinding(InputKey.Semicolon, ShredAction.Row1Pick3),
            new KeyBinding(Input.ShredDefaultKeys.FingerPick, ShredAction.Row1Pick4),
            new KeyBinding(InputKey.Z, ShredAction.Row2Fret1),
            new KeyBinding(InputKey.X, ShredAction.Row2Fret2),
            new KeyBinding(InputKey.C, ShredAction.Row2Fret3),
            new KeyBinding(InputKey.V, ShredAction.Row2Fret4),
            new KeyBinding(InputKey.B, ShredAction.Row2Fret5),
            new KeyBinding(InputKey.M, ShredAction.Row2Pick1),
            new KeyBinding(InputKey.Comma, ShredAction.Row2Pick2),
            new KeyBinding(InputKey.Period, ShredAction.Row2Pick3),
            new KeyBinding(InputKey.Slash, ShredAction.Row2Pick4),
            new KeyBinding(InputKey.Space, ShredAction.Wham),
            new KeyBinding(InputKey.F3, ShredAction.ScrollFaster),
            new KeyBinding(InputKey.F4, ShredAction.ScrollSlower),

        };
            return bindings;
        }

        public override Drawable CreateIcon() => new ShredRulesetIcon
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Text = ShortName[0].ToString(),
            Font = OsuFont.Default.With(size: 18),
        };

        // Leave this line intact. It will bake the correct version into the ruleset on each build/release.
        public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;
    }
}
