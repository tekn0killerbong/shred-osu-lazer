using System.Security.Cryptography;
using osu.Framework.Audio.Sample;
using osu.Game.Skinning;
using osu.Game.Models;
using osu.Game.Rulesets.Shred.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.Compatibility;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Rulesets.Shred.Skinning;
using osu.Game.Rulesets.Objects.Legacy;

internal static class Revision45Checks
{
	public static readonly (string name, string hash, double ms)[] Files =
	{
		("drum-hitclap", "ea40ce0bf521c96a4de67e7026edbc80dabe86d0b7ba06425c18c022181fd1ab", 1323.990929705215),
		("drum-hitfinish", "517fec0bc24ee54ef25c586d7af2db877d674ba29d961e6c6b2dd161991c40a4", 1370.272108843537),
		("drum-hitnormal", "d9f73d7b8c4391d1c7c9612a99e93b569490b7112b21aaab400516a65325590e", 1562.879818594104),
		("drum-hitwhistle", "3b7501a2b1eaf9093defcaf221b18d56ee806c411a5623ce639e4127e078fd6f", 1224.489795918367),
		("normal-hitclap", "5abaf7fbc747754a96fa0ffb697cf280b4650541364c6d10aa6f78daf7d006ca", 1632.698412698413),
		("normal-hitfinish", "b945baedb8816d780904048a6e6cd58c3b818146842abc1c30f66340489f841f", 2003.605442176871),
		("normal-hitnormal", "f34643fa0a6b1b44cf0e0ad7cd2356c376bff8afdb0664eb5b2dab17c0e2fe57", 1442.426303854875),
		("normal-hitwhistle", "878e67b1c08fbf016a0386220eb62e9981d171c04d1e4736f27ce4948504b8aa", 1265.804988662132),
		("soft-hitclap", "628697429b388ff39b2a3250f8dc2bba83f3c67d1c5bb694c7e86a9e4d805773", 1144.195011337868),
		("soft-hitfinish", "869bce5cd484883fe920134dda187bd19c84e5211512950e14d41646a15c10a0", 1291.859410430839),
		("soft-hitnormal", "5a51a71fd693aa3beed4fc07775a4d6c07f74228fac29234dc591417a0b61069", 1150.385487528345),
		("soft-hitwhistle", "7d7f2993a02a56353d7e66ce883e9d6481416359112bccb978dd7a0d898b55b3", 1225.873015873016),
	};
	public static void Check(bool ok, string label)
	{
		if (!ok) throw new Exception(label);
		Console.WriteLine("PASS v45 " + label);
	}
	public static void Run()
	{
		var ruleset = new ShredRuleset();
		using var resources = ruleset.CreateResourceStore();
		foreach (var file in Files)
		{
			var data = resources.Get("Samples/Gameplay/" + file.name + ".wav");
			Check(data != null && Convert.ToHexString(SHA256.HashData(data)).Equals(file.hash, StringComparison.OrdinalIgnoreCase),
				file.name + " bundled WAV has the expected hash (Drum repacked without DSP in v63)");
		}
		Check(!ShredDefaultSkinTransformer.HasBundledSample(new ConvertHitObjectParser.FileHitSampleInfo("custom.wav", 100)), "file hitsounds retain native lookup");
		Check(!ShredDefaultSkinTransformer.HasBundledSample(new HitSampleInfo("sliderslide")), "other gameplay sounds retain native lookup");
		using (var skin = new ProbeBuiltinSkin())
		{
			var transformer = ruleset.CreateSkinTransformer(skin, new global::osu.Game.Beatmaps.Beatmap());
			var normal = new HitSampleInfo(HitSampleInfo.HIT_NORMAL);
			Check(transformer.GetSample(normal) == null, "missing built-in hit yields to bundled Shred resources");
			skin.SkinInfo.Value.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = "test" }, "Gameplay/normal-hitnormal.wav"));
			Check(transformer.GetSample(normal) == null, "user-edited global skin yields bank sounds to Shred guitars");
			Check(ReferenceEquals(transformer.GetSample(new HitSampleInfo("sliderslide")), skin.CustomSample), "unrelated skin audio remains unchanged");
			Check(ReferenceEquals(transformer.GetSample(new ConvertHitObjectParser.FileHitSampleInfo("custom.wav", 100)), skin.CustomSample), "custom filename remains unchanged");
		}

		var defaults = ruleset.GetDefaultKeyBindings(Ruleset.EDITOR_VARIANT).ToArray();
		var actions = defaults.Select(b => (ShredEditorAction)b.Action).ToArray();
		Check(Array.IndexOf(actions, ShredEditorAction.DecreaseGridSpacing) < Array.IndexOf(actions, ShredEditorAction.IncreaseGridSpacing)
			&& Array.IndexOf(actions, ShredEditorAction.IncreaseGridType) < Array.IndexOf(actions, ShredEditorAction.DecreaseGridType), "settings list Increase before Decrease");
		foreach (var pair in new[] { (ShredEditorAction.DecreaseGridSpacing, InputKey.I), (ShredEditorAction.IncreaseGridSpacing, InputKey.O),
			(ShredEditorAction.IncreaseGridType, InputKey.P), (ShredEditorAction.DecreaseGridType, ShredDefaultKeys.ModeIncrease) })
			Check(defaults.First(b => (ShredEditorAction)b.Action == pair.Item1).KeyCombination.Equals(new KeyCombination(pair.Item2)), "default " + pair.Item1 + " = " + pair.Item2);

		using var storage = new TemporaryNativeStorage("shred-v45-migration");
		using var access = new RealmAccess(storage, "migration.realm");
		ShredGridBindingMigration.Apply(access, storage); // No premature marker before bindings exist.
		access.Write(r =>
		{
			r.Add(new RealmKeyBinding((int)ShredEditorAction.DecreaseGridSpacing, new KeyCombination(InputKey.O), "shred", Ruleset.EDITOR_VARIANT));
			r.Add(new RealmKeyBinding((int)ShredEditorAction.IncreaseGridSpacing, new KeyCombination(InputKey.I), "shred", Ruleset.EDITOR_VARIANT));
			r.Add(new RealmKeyBinding((int)ShredEditorAction.IncreaseGridType, new KeyCombination(ShredDefaultKeys.ModeIncrease), "shred", Ruleset.EDITOR_VARIANT));
			r.Add(new RealmKeyBinding((int)ShredEditorAction.DecreaseGridType, new KeyCombination(InputKey.P), "shred", Ruleset.EDITOR_VARIANT));
			r.Add(new RealmKeyBinding((int)ShredEditorAction.DecreaseGridSpacing, new KeyCombination(InputKey.O), "mania", Ruleset.EDITOR_VARIANT));
		});
		ShredGridBindingMigration.Apply(access, storage);
		access.Run(r =>
		{
			foreach (var binding in r.All<RealmKeyBinding>().Where(b => b.RulesetName == "shred"))
				Check(binding.KeyCombination.Equals(defaults.First(b => (int)(ShredEditorAction)b.Action == binding.ActionInt).KeyCombination), "persisted v44 binding migrated: " + binding.ActionInt);
			Check(r.All<RealmKeyBinding>().Single(b => b.RulesetName == "mania").KeyCombination.Equals(new KeyCombination(InputKey.O)), "other ruleset binding preserved");
		});
		access.Write(r =>
		{
			foreach (var binding in r.All<RealmKeyBinding>().Where(b => b.RulesetName == "shred"))
			{
				if (binding.ActionInt == (int)ShredEditorAction.DecreaseGridSpacing) binding.KeyCombination = new KeyCombination(InputKey.O);
				if (binding.ActionInt == (int)ShredEditorAction.IncreaseGridSpacing) binding.KeyCombination = new KeyCombination(InputKey.I);
			}
		});
		ShredGridBindingMigration.Apply(access, storage);
		access.Run(r => Check(r.All<RealmKeyBinding>().Single(b => b.RulesetName == "shred" && b.ActionInt == (int)ShredEditorAction.DecreaseGridSpacing)
			.KeyCombination.Equals(new KeyCombination(InputKey.O)), "one-time migration preserves later user rebinding"));
		var custom = new[] {
			new RealmKeyBinding((int)ShredEditorAction.DecreaseGridSpacing, new KeyCombination(InputKey.F8), "shred", Ruleset.EDITOR_VARIANT),
			new RealmKeyBinding((int)ShredEditorAction.IncreaseGridSpacing, new KeyCombination(InputKey.I), "shred", Ruleset.EDITOR_VARIANT) };
		ShredGridBindingMigration.SwapFormerDefaults(custom, ShredEditorAction.DecreaseGridSpacing, ShredEditorAction.IncreaseGridSpacing, InputKey.O, InputKey.I);
		Check(custom[0].KeyCombination.Equals(new KeyCombination(InputKey.F8)) && custom[1].KeyCombination.Equals(new KeyCombination(InputKey.I)), "custom pair is not reset");
	}
	private sealed class ProbeBuiltinSkin : TrianglesSkin
	{
		public readonly ISample CustomSample = new SampleVirtual();
		public ProbeBuiltinSkin() : base(new SkinInfo("user-edited", "test"), null) { }
		public override ISample GetSample(ISampleInfo sampleInfo) => CustomSample;
	}
}
