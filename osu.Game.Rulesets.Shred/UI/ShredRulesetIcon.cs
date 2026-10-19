using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;

namespace osu.Game.Rulesets.Shred.UI
{
	public partial class ShredRulesetIcon : Sprite
	{
		private static readonly ConditionalWeakTable<FontStore, object> registered = new ConditionalWeakTable<FontStore, object>();
        private static readonly ConditionalWeakTable<TextureStore, object> registeredTextures = new ConditionalWeakTable<TextureStore, object>();
        [BackgroundDependencyLoader(true)]
        private void load(FontStore fonts, TextureStore textures, osu.Game.Database.RealmAccess realm, osu.Framework.Platform.Storage storage
#if SHRED_OFFICIAL
            , osu.Game.Beatmaps.BeatmapManager manager,
            osu.Framework.Platform.GameHost game, osu.Framework.Bindables.Bindable<osu.Game.Beatmaps.WorkingBeatmap> selected, OsuGame osuGame
#endif
            )
		{
            if (realm != null && storage != null) Compatibility.ShredGridBindingMigration.Apply(realm, storage);
#if SHRED_OFFICIAL
            if (osuGame != null && game != null)
                Compatibility.ShredEditorTestPlayback.Install(osuGame, game);
            if (manager != null && storage != null)
                Compatibility.ShredLegacyExport.Install(manager, storage);
            if (manager != null && realm != null && game != null && selected != null)
                Compatibility.ShredAutoImport.Install(manager, realm, game, selected);
#endif
            lock (registeredTextures)
            {
                if (!registeredTextures.TryGetValue(textures, out _))
                {
                    textures.AddTextureSource(new ShredLogoTextureLoaderStore());
                    registeredTextures.Add(textures, new object());
                }
            }
            // Load a pre-filtered copy of the complete logo, cached by the host texture store.
            Texture = textures.Get(ShredLogoTextureLoaderStore.ResourceName);
            FillMode = FillMode.Fit;
			lock (registered)
			{
				if (registered.TryGetValue(fonts, out _)) return;
				fonts.AddTextureSource(new GlyphStore(new ResourceStore<byte[]>(new DllResourceStore(typeof(ShredRuleset).Assembly)), "Resources/Fonts/ShredMods"));
				registered.Add(fonts, new object());
			}
		}
	}
}
