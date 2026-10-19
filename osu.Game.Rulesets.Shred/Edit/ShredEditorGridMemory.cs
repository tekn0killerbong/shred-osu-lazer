using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;

namespace osu.Game.Rulesets.Shred.Edit
{
	/// <summary>Local editor preferences, independent of saving the beatmap itself.</summary>
	internal sealed class ShredEditorGridMemory : IDisposable
	{
		private const string directory = "shred-editor-grids";
		private readonly Storage storage;
		private readonly string path;
		private readonly BindableBeatDivisor divisor;
		private readonly Action<Action> schedule;
		private GridSetting pending;
		private GridSetting saved;
		private bool queued;
		private bool disposed;

		public ShredEditorGridMemory(Storage storage, Guid beatmapId, BindableBeatDivisor divisor, Action<Action> schedule)
		{
			this.storage = storage;
			this.divisor = divisor;
			this.schedule = schedule;
			if (beatmapId == Guid.Empty) return;
			path = $"{directory}/{beatmapId:N}.json";
			try
			{
				using Stream stream = storage.GetStream(path);
				if (stream != null)
				{
					GridSetting setting = JsonSerializer.Deserialize<GridSetting>(stream);
					if (tryGetPresets(setting, out BeatDivisorPresetCollection presets))
					{
						// Propagate the preset to every bound sidebar control before the value.
						divisor.ValidDivisors.Value = presets;
						divisor.Value = setting.Divisor;
						saved = setting;
					}
				}
			}
			catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException)
			{
				Logger.Error(error, "Shred could not read the saved editor grid; using the beatmap grid.");
			}
			capture();
			divisor.ValueChanged += spacingChanged;
			divisor.ValidDivisors.ValueChanged += presetsChanged;
		}

		private static bool tryGetPresets(GridSetting setting, out BeatDivisorPresetCollection presets)
		{
			presets = null;
			if (setting == null || setting.Divisor < 1 || setting.Divisor > 64 || setting.Maximum < 1 || setting.Maximum > 64)
				return false;
			switch (setting.Type)
			{
				case BeatDivisorType.Common:
					presets = BeatDivisorPresetCollection.COMMON;
					break;
				case BeatDivisorType.Triplets:
					presets = BeatDivisorPresetCollection.TRIPLETS;
					break;
				case BeatDivisorType.Custom:
					presets = BeatDivisorPresetCollection.Custom(setting.Maximum);
					break;
				default:
					return false;
			}
			return presets.Presets.Contains(setting.Divisor);
		}

		private void capture()
		{
			BeatDivisorPresetCollection presets = divisor.ValidDivisors.Value;
			pending = new GridSetting(divisor.Value, presets.Type, presets.Presets.Max());
		}

		private void spacingChanged(ValueChangedEvent<int> change) => changed();
		private void presetsChanged(ValueChangedEvent<BeatDivisorPresetCollection> change) => changed();
		private void changed()
		{
			capture();
			if (queued || disposed) return;
			queued = true;
			schedule(() =>
			{
				queued = false;
				if (disposed) return;
				// A preset change can emit several intermediate spacing values.
				capture();
				flush();
			});
		}

		private void flush()
		{
			if (path == null || pending == saved || !tryGetPresets(pending, out _)) return;
			try
			{
				using (Stream stream = storage.CreateFileSafely(path))
					JsonSerializer.Serialize(stream, pending);
				saved = pending;
			}
			catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
			{
				Logger.Error(error, "Shred could not save the editor grid preference.");
			}
		}

		public void Dispose()
		{
			if (disposed) return;
			disposed = true;
			divisor.ValueChanged -= spacingChanged;
			divisor.ValidDivisors.ValueChanged -= presetsChanged;
			flush();
		}

		private sealed record GridSetting(int Divisor, BeatDivisorType Type, int Maximum);
	}
}
