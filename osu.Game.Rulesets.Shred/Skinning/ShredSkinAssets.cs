using System.Collections.Generic;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Game.Skinning;
namespace osu.Game.Rulesets.Shred.Skinning
{
	public sealed class ShredSkinAssets : System.IDisposable
	{
		private readonly TextureStore local;
		private readonly ISkinSource source;
		private readonly Dictionary<string, Texture[]> cache = new Dictionary<string, Texture[]>();
		public ShredSkinAssets(ShredSkinSettings settings, GameHost host, ISkinSource source)
		{
			this.source = source;
			if (settings.Folder != null)
				local = new TextureStore(host.Renderer, new TextureLoaderStore(new StorageBackedResourceStore(new NativeStorage(settings.Folder, host))), scaleAdjust: 1);
		}
		public Texture[] GetFrames(string name)
		{
			if (cache.TryGetValue(name, out Texture[] cached)) return cached;
			// Keep every frame of an animation in one source (local first, active skin second).
			Texture[] frames = load(name, true);
			if (frames.Length == 0) frames = load(name, false);
			cache[name] = frames;
			return frames;
		}
		private Texture[] load(string name, bool fromLocal)
		{
			List<Texture> frames = new List<Texture>();
			for (int index = 0; index < 120; index++)
			{
				Texture texture = get(name + "-" + index, fromLocal);
				if (texture == null) break;
				frames.Add(texture);
			}
			if (frames.Count == 0)
			{
				Texture texture = get(name, fromLocal);
				if (texture != null) frames.Add(texture);
			}
			return frames.ToArray();
		}
		private Texture get(string name, bool fromLocal)
		{
			if (fromLocal) return local?.Get(name + ".png");
			return source?.GetTexture(name);
		}
		public void Dispose() => local?.Dispose();
	}
}
