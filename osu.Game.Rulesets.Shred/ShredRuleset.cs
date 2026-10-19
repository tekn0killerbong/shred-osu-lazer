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
    public partial class ShredRuleset : Ruleset
#if !SHRED_OFFICIAL
        , ILegacyBeatmapExtension
#endif
    {
        static ShredRuleset()
        {
#if SHRED_OFFICIAL
            ShredPortableDecoder.Register();
#endif
        }
        public override osu.Game.Rulesets.Edit.HitObjectComposer CreateHitObjectComposer()
        {
            return new Edit.ShredHitObjectComposer(this);
        }

        public override IEnumerable<Drawable> CreateEditorSetupSections() => new Drawable[]
        {
            new osu.Game.Screens.Edit.Setup.MetadataSection(),
            new Edit.ShredDifficultySection(),
            new osu.Game.Screens.Edit.Setup.ResourcesSection()
        };

#if SHRED_OFFICIAL
        // Keep discovery custom (-1); only instantiated sessions implement the
        // official editor's legacy save gate. Nested types are not plugin candidates.
        public ShredRuleset()
        {
            RulesetInfo.OnlineID = -1;
            RulesetInfo.InstantiationInfo = $"{typeof(EditorCompatibleShredRuleset).FullName}, {typeof(ShredRuleset).Assembly.GetName().Name}";
        }

        public sealed class EditorCompatibleShredRuleset : ShredRuleset, ILegacyRuleset
        {
            int ILegacyRuleset.LegacyID => 0;
            osu.Game.Rulesets.Scoring.Legacy.ILegacyScoreSimulator ILegacyRuleset.CreateLegacyScoreSimulator()
                => new Compatibility.ShredLegacyScoreSimulator();
        }

        public override IBeatmapProcessor CreateBeatmapProcessor(IBeatmap beatmap)
            => Compatibility.ShredLegacyEditorBridge.CreateProcessor(beatmap);
#else
        public override IBeatmapProcessor CreateBeatmapProcessor(IBeatmap beatmap)
        {
            if (beatmap is osu.Game.Screens.Edit.EditorBeatmap) return new ShredEditorBeatmapProcessor(beatmap);
            return null;
        }
#endif

        public override string Description => "shred";

        public override osu.Game.Overlays.Settings.RulesetSettingsSubsection CreateSettings() => new Edit.ShredEditorSettings(this);

        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod> mods = null) => new DrawableShredRuleset(this, beatmap, mods);

        public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) => new ShredBeatmapConverter(beatmap, this);

        public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new ShredDifficultyCalculator(RulesetInfo, beatmap);

        public override osu.Game.Skinning.ISkin CreateSkinTransformer(osu.Game.Skinning.ISkin skin, IBeatmap beatmap)
        {
            // Global osu! skins must not replace the ruleset's guitar banks.
            // Keep beatmap-specific sounds on the normal authored-audio path.
            if (skin is osu.Game.Skinning.TrianglesSkin || skin is osu.Game.Skinning.ArgonSkin
                || (skin is osu.Game.Skinning.LegacySkin && skin is not osu.Game.Skinning.LegacyBeatmapSkin))
                return new Skinning.ShredDefaultSkinTransformer((osu.Game.Skinning.Skin)skin);
            return null;
        }

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
            double rate = ModUtils.CalculateRateWithMods(mods);
            yield return Scoring.ShredAccuracyDisplay.Create(info.Difficulty.OverallDifficulty, adjusted.OverallDifficulty, rate);
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
                    new KeyBinding(new[] { InputKey.Control, InputKey.Q }, Edit.ShredEditorAction.ToggleStepRecorder),
                    new KeyBinding(Input.ShredDefaultKeys.EmptyStep, Edit.ShredEditorAction.EmptyStep),
                    new KeyBinding(InputKey.Right, Edit.ShredEditorAction.EmptyStep),
                    new KeyBinding(InputKey.Number7, Edit.ShredEditorAction.DecreaseGridSpacing),
                    new KeyBinding(InputKey.None, Edit.ShredEditorAction.DecreaseGridSpacing),
                    new KeyBinding(InputKey.Number8, Edit.ShredEditorAction.IncreaseGridSpacing),
                    new KeyBinding(InputKey.None, Edit.ShredEditorAction.IncreaseGridSpacing),
                    new KeyBinding(InputKey.Number9, Edit.ShredEditorAction.DecreaseGridType),
                    new KeyBinding(InputKey.None, Edit.ShredEditorAction.DecreaseGridType),
                    new KeyBinding(InputKey.Number0, Edit.ShredEditorAction.IncreaseGridType),
                    new KeyBinding(InputKey.None, Edit.ShredEditorAction.IncreaseGridType)
                };
            List<KeyBinding> bindings = new List<KeyBinding>
        {
            new KeyBinding(InputKey.A, ShredAction.Row1Fret1),
            new KeyBinding(InputKey.S, ShredAction.Row1Fret2),
            new KeyBinding(InputKey.D, ShredAction.Row1Fret3),
            new KeyBinding(InputKey.F, ShredAction.Row1Fret4),
            new KeyBinding(InputKey.G, ShredAction.Row1Fret5),
            new KeyBinding(InputKey.Z, ShredAction.Row2Fret1),
            new KeyBinding(InputKey.X, ShredAction.Row2Fret2),
            new KeyBinding(InputKey.C, ShredAction.Row2Fret3),
            new KeyBinding(InputKey.V, ShredAction.Row2Fret4),
            new KeyBinding(InputKey.B, ShredAction.Row2Fret5),
            new KeyBinding(InputKey.K, ShredAction.Row1Pick1),
            new KeyBinding(InputKey.L, ShredAction.Row1Pick2),
            new KeyBinding(InputKey.Semicolon, ShredAction.Row1Pick3),
            new KeyBinding(Input.ShredDefaultKeys.FingerPick, ShredAction.Row1Pick4),
            new KeyBinding(InputKey.M, ShredAction.Row2Pick1),
            new KeyBinding(InputKey.Comma, ShredAction.Row2Pick2),
            new KeyBinding(InputKey.Period, ShredAction.Row2Pick3),
            new KeyBinding(InputKey.Slash, ShredAction.Row2Pick4),
            new KeyBinding(InputKey.Space, ShredAction.Wham),
            new KeyBinding(InputKey.F3, ShredAction.ScrollSlower),
            new KeyBinding(InputKey.F4, ShredAction.ScrollFaster),

        };
            return bindings;
        }

        public override Drawable CreateIcon() => new ShredRulesetIcon
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            // ConstrainedIconContainer preserves this drawable's aspect ratio.
            // The supplied logo is square; a rectangular Size stretches its texture.
            Size = new osuTK.Vector2(22),
        };

        // Leave this line intact. It will bake the correct version into the ruleset on each build/release.
        public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;
    }
}
