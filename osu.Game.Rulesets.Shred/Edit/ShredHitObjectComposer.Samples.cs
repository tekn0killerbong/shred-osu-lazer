using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Audio;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Shred.Objects;

namespace osu.Game.Rulesets.Shred.Edit
{
	public partial class ShredHitObjectComposer
	{
		// The official client's filename carrier is persistence data, not an editable
		// FileHitSampleInfo. Native buttons must operate on the decoded audio samples.
		private readonly Dictionary<string, Bindable<TernaryState>> shredSamples = states(HitSampleInfo.ALL_ADDITIONS);
		private readonly Dictionary<string, Bindable<TernaryState>> shredBanks = states(HitSampleInfo.ALL_BANKS.Prepend(HIT_BANK_AUTO));
		private readonly Dictionary<string, Bindable<TernaryState>> shredAdditionBanks = states(HitSampleInfo.ALL_BANKS.Prepend(HIT_BANK_AUTO));
		private bool readingSamples;
		private bool samplesReady;
		public override IReadOnlyDictionary<string, Bindable<TernaryState>> SelectionSampleStates => shredSamples;
		public override IReadOnlyDictionary<string, Bindable<TernaryState>> SelectionBankStates => shredBanks;
		public override IReadOnlyDictionary<string, Bindable<TernaryState>> SelectionAdditionBankStates => shredAdditionBanks;

		private static Dictionary<string, Bindable<TernaryState>> states(IEnumerable<string> names)
			=> names.ToDictionary(name => name, name => new Bindable<TernaryState> { Description = name });

		public static IList<HitSampleInfo> EditableSamples(HitObject note)
		{
#if SHRED_OFFICIAL
			return Compatibility.ShredEditorCodec.PlayableSamples(note.Samples);
#else
			return note.Samples.Select(s => s.With()).ToList();
#endif
		}

		[BackgroundDependencyLoader]
		private void loadSampleEditing()
		{
			foreach (var pair in shredSamples)
				pair.Value.BindValueChanged(change =>
				{
					if (readingSamples || change.NewValue == TernaryState.Indeterminate) return;
					if (change.NewValue == TernaryState.True) AddHitSample(pair.Key);
					else RemoveHitSample(pair.Key);
					InvokeSelectionStateChanged();
				});
			foreach (var pair in shredBanks)
				pair.Value.BindValueChanged(change => bankChanged(pair.Key, change.NewValue, false));
			foreach (var pair in shredAdditionBanks)
				pair.Value.BindValueChanged(change => bankChanged(pair.Key, change.NewValue, true));
			samplesReady = true;
			EditorBeatmap.SelectedHitObjects.CollectionChanged += sampleSelectionChanged;
			resetSampleChoices();
			UpdateTernaryStates();
		}

		private void sampleSelectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (EditorBeatmap.SelectedHitObjects.Count == 0) resetSampleChoices();
			Scheduler.AddOnce(UpdateTernaryStates);
		}

		private void resetSampleChoices()
		{
			readingSamples = true;
			foreach (var pair in shredSamples) pair.Value.Value = TernaryState.False;
			selectBank(shredBanks, HIT_BANK_AUTO);
			selectBank(shredAdditionBanks, HIT_BANK_AUTO);
			readingSamples = false;
			InvokeSelectionStateChanged();
		}

		private static void selectBank(Dictionary<string, Bindable<TernaryState>> choices, string bank)
		{
			foreach (var pair in choices)
			{
				pair.Value.Value = TernaryState.False;
				if (pair.Key == bank) pair.Value.Value = TernaryState.True;
			}
		}

		private void bankChanged(string bank, TernaryState value, bool addition)
		{
			if (readingSamples || value == TernaryState.Indeterminate) return;
			var choices = shredBanks;
			if (addition) choices = shredAdditionBanks;
			if (value == TernaryState.True)
			{
				readingSamples = true;
				selectBank(choices, bank);
				readingSamples = false;
				if (addition) SetSampleAdditionBank(bank);
				else if (bank != HIT_BANK_AUTO) SetSampleBank(bank);
			}
			else if (choices.Values.All(v => v.Value == TernaryState.False))
			{
				readingSamples = true;
				choices[bank].Value = TernaryState.True;
				readingSamples = false;
			}
			UpdateTernaryStates();
			InvokeSelectionStateChanged();
		}

		private static TernaryState stateOf(IEnumerable<bool> matches)
		{
			bool[] values = matches.Distinct().ToArray();
			if (values.Length > 1) return TernaryState.Indeterminate;
			if (values.Length == 1 && values[0]) return TernaryState.True;
			return TernaryState.False;
		}

		protected override void UpdateTernaryStates()
		{
			if (!samplesReady || readingSamples) return;
			AutoSelectionBankEnabled.Value = EditorBeatmap.SelectedHitObjects.Count == 0;
			var selection = EditorBeatmap.SelectedHitObjects.Select(EditableSamples).ToArray();
			if (selection.Length == 0) return;
			readingSamples = true;
			try
			{
				foreach (var pair in shredSamples)
					pair.Value.Value = stateOf(selection.Select(samples => samples.Any(s => s.Name == pair.Key)));
				foreach (var pair in shredBanks)
					pair.Value.Value = stateOf(selection.SelectMany(s => s).Where(s => s.Name == HitSampleInfo.HIT_NORMAL).Select(s => s.Bank == pair.Key));
				var additions = selection.SelectMany(s => s).Where(s => s.Name != HitSampleInfo.HIT_NORMAL).ToArray();
				if (additions.Length > 0)
					foreach (var pair in shredAdditionBanks)
						pair.Value.Value = stateOf(additions.Select(s => (pair.Key == HIT_BANK_AUTO && s.EditorAutoBank)
							|| (pair.Key != HIT_BANK_AUTO && s.Bank == pair.Key && !s.EditorAutoBank)));
			}
			finally { readingSamples = false; }
		}

		private void editSamples(Action<ShredHitObject> edit)
		{
			EditorBeatmap.PerformOnSelection(hitObject =>
			{
				if (hitObject is not ShredHitObject note) return;
				// Edit a detached sample list and publish the final result once. Every
				// assignment on the live note reloads its audio; decoding, editing and
				// encoding in place used to trigger several reloads for each click.
				var draft = new ShredHitObject { Samples = EditableSamples(note) };
				edit(draft);
#if SHRED_OFFICIAL
				Compatibility.ShredEditorCodec.Synchronise(note, draft.Samples);
#else
				note.Samples = draft.Samples;
#endif
			});
			UpdateTernaryStates();
			queueAudioDiagnostic("sample-edit");
		}

		public new void SetSampleBank(string bank) => editSamples(note =>
			note.Samples = note.Samples.Select(s =>
			{
				if (s.Name == HitSampleInfo.HIT_NORMAL || s.EditorAutoBank) return s.With(newBank: bank);
				return s;
			}).ToList());

		public new void SetSampleAdditionBank(string bank) => editSamples(note =>
		{
			string normal = note.Samples.FirstOrDefault(s => s.Name == HitSampleInfo.HIT_NORMAL)?.Bank ?? HitSampleInfo.BANK_NORMAL;
			string target = bank;
			if (bank == HIT_BANK_AUTO) target = normal;
			note.Samples = note.Samples.Select(s =>
			{
				if (s.Name != HitSampleInfo.HIT_NORMAL) return s.With(newBank: target, newEditorAutoBank: bank == HIT_BANK_AUTO);
				return s;
			}).ToList();
		});

		public new void AddHitSample(string name) => editSamples(note =>
		{
			if (note.Samples.Any(s => s.Name == name)) return;
			HitSampleInfo sample = note.CreateHitSampleInfo(name);
			if (sample.Name != name) sample = additionFrom(sample, name);
			string bank = shredAdditionBanks.FirstOrDefault(p => p.Value.Value == TernaryState.True).Key;
			if (bank != null && bank != HIT_BANK_AUTO) sample = sample.With(newBank: bank, newEditorAutoBank: false);
			note.Samples.Add(sample);
		});

		public new void RemoveHitSample(string name) => editSamples(note =>
			note.Samples = note.Samples.Where(s => s.Name != name).ToList());

		private static HitSampleInfo additionFrom(HitSampleInfo template, string name)
			=> new HitSampleInfo(name, template.Bank, template.Suffix, template.Volume, true, template.UseBeatmapSamples);

		public IList<HitSampleInfo> CreateRecorderSamples(double time)
		{
			HitObject previous = EditorBeatmap.HitObjects.LastOrDefault(n => n.StartTime < time);
			HitSampleInfo template = null;
			if (previous != null) template = EditableSamples(previous).FirstOrDefault(s => s.Name == HitSampleInfo.HIT_NORMAL);
			template ??= new HitSampleInfo(HitSampleInfo.HIT_NORMAL);
			string bank = shredBanks.FirstOrDefault(p => p.Value.Value == TernaryState.True).Key;
			if (bank != null && bank != HIT_BANK_AUTO) template = template.With(newBank: bank, newEditorAutoBank: false);
			var result = new List<HitSampleInfo> { template.With() };
			string additionBank = shredAdditionBanks.FirstOrDefault(p => p.Value.Value == TernaryState.True).Key;
			foreach (var pair in shredSamples.Where(p => p.Value.Value == TernaryState.True))
			{
				var sample = template.With(newName: pair.Key, newEditorAutoBank: true);
				if (sample.Name != pair.Key) sample = additionFrom(template, pair.Key);
				if (additionBank != null && additionBank != HIT_BANK_AUTO) sample = sample.With(newBank: additionBank, newEditorAutoBank: false);
				result.Add(sample);
			}
			return result;
		}

		protected override void Dispose(bool isDisposing)
		{
			if (samplesReady) EditorBeatmap.SelectedHitObjects.CollectionChanged -= sampleSelectionChanged;
			base.Dispose(isDisposing);
		}
	}
}
