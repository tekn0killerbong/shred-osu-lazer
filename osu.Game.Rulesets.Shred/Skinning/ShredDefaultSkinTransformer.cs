using System.Linq;
using osu.Framework.Audio.Sample;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Shred.Skinning
{
	/// <summary>Keep Shred's guitar banks independent of the global osu! skin.</summary>
	public sealed class ShredDefaultSkinTransformer : SkinTransformer
	{
		public ShredDefaultSkinTransformer(Skin skin) : base(skin)
		{
		}

		public static bool HasBundledSample(ISampleInfo sampleInfo)
		{
			if (sampleInfo is not HitSampleInfo hit || hit is ConvertHitObjectParser.FileHitSampleInfo) return false;
			if (!HitSampleInfo.ALL_BANKS.Contains(hit.Bank)) return false;
			return hit.Name == HitSampleInfo.HIT_NORMAL || HitSampleInfo.ALL_ADDITIONS.Contains(hit.Name);
		}

		public override ISample GetSample(ISampleInfo sampleInfo)
		{
			if (!HasBundledSample(sampleInfo)) return base.GetSample(sampleInfo);

			// Built-in and imported global skins sit before ResourceStoreBackedSkin.
			// Yield all twelve ordinary bank sounds to Shred's embedded guitar WAVs,
			// including when a mania/standard skin has its own bank-specific files.
			// Beatmap skins are deliberately not wrapped by this transformer, so
			// authored map audio and explicit filename samples still work normally.
			return null;
		}
	}
}
