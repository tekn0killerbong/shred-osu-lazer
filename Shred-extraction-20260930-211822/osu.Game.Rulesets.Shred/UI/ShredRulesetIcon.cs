using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;

namespace osu.Game.Rulesets.Shred.UI
{
	public partial class ShredRulesetIcon : SpriteText
	{
		private static readonly ConditionalWeakTable<FontStore, object> registered = new ConditionalWeakTable<FontStore, object>();
		[BackgroundDependencyLoader]
		private void load(FontStore fonts)
		{
			lock (registered)
			{
				if (registered.TryGetValue(fonts, out _)) return;
				fonts.AddTextureSource(new GlyphStore(new ResourceStore<byte[]>(new DllResourceStore(typeof(ShredRuleset).Assembly)), "Resources/Fonts/ShredMods"));
				registered.Add(fonts, new object());
			}
		}
	}
}
