using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Rulesets.Shred.UI
{
	/// <summary>
	/// Pre-filters the large source logo for small ruleset icons before GPU mipmapping.
	/// </summary>
	public sealed class ShredLogoTextureLoaderStore : ResourceStore<TextureUpload>
	{
		public const string ResourceName = "Resources/shred-logo.png";
		public const int TextureSize = 160;

		public ShredLogoTextureLoaderStore()
			: base(new TextureLoaderStore(new DllResourceStore(typeof(ShredRuleset).Assembly)))
		{
		}

		public override TextureUpload Get(string name)
		{
			// Do not resize mod glyphs or any other embedded images.
			if (name != ResourceName)
				return null;

			using var original = base.Get(name);
			if (original == null)
				return null;

			var image = Image.LoadPixelData(original.Data, original.Width, original.Height);
			try
			{
				// Framework 2026.914 limits automatic mipmaps to three levels.
				// Starting at 160 gives a 20px final mip matching the official toolbar.
				// Lanczos integrates the source coverage instead of sampling a few of its pixels.
				image.Mutate(context => context.Resize(new ResizeOptions
				{
					Size = new Size(TextureSize, TextureSize),
					Mode = ResizeMode.Max,
					Sampler = KnownResamplers.Lanczos3,
					PremultiplyAlpha = true,
				}));
				return new TextureUpload(image);
			}
			catch
			{
				image.Dispose();
				throw;
			}
		}

		public override Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = default)
			=> Task.Run(() => Get(name), cancellationToken);
	}
}
