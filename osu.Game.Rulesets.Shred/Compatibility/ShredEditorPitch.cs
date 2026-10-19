#if SHRED_OFFICIAL
using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Lists;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Edit;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.Components.Timelines.Summary;

namespace osu.Game.Rulesets.Shred.Compatibility
{
	internal partial class ShredEditorPitch : Component
	{
		private readonly EditorClock clock;
		private readonly IBindable<double> speed;
		private readonly PlaybackPitchControl header;
		private readonly BindableDouble tempo = new BindableDouble(1);
		private readonly BindableDouble frequency = new BindableDouble(1);

		private ShredEditorPitch(EditorClock clock, IBindable<double> speed, PlaybackPitchControl header)
		{
			this.clock = clock;
			this.speed = speed;
			this.header = header;
		}

		public static void Install(Editor editor)
		{
			if (!IsShredEditor(editor)) return;
			PlaybackControl playback = editor.ChildrenOfType<PlaybackControl>().SingleOrDefault();
			if (playback == null || playback.ChildrenOfType<PlaybackPitchControl>().Any()) return;
			PlaybackControl.PlaybackTabControl tabs = playback.ChildrenOfType<PlaybackControl.PlaybackTabControl>().SingleOrDefault();
			if (tabs?.Parent is not FillFlowContainer flow) return;
			OsuSpriteText label = flow.Children.OfType<OsuSpriteText>().SingleOrDefault();
			if (label == null) return;

			// The supported native BottomBar has four columns. Verify its structure before
			// replacing the setter-only dimensions, preserving the other three columns.
			bool resized = false;
			for (Drawable parent = playback.Parent; parent != null && !resized; parent = parent.Parent)
			{
				if (parent is not GridContainer grid) continue;
				foreach (ObservableArray<Drawable> row in grid.Content)
				{
					Drawable[] cells = row.ToArray();
					if (cells.Length != 4 || cells[0] is not TimeInfoContainer || cells[1] is not SummaryTimeline
						|| cells[2] != playback || cells[3] is not TestGameplayButton) continue;
					grid.ColumnDimensions = new[]
					{
						new Dimension(GridSizeMode.Absolute, 150),
						new Dimension(),
						new Dimension(GridSizeMode.Absolute, PlaybackPitchControl.PlaybackColumnWidth),
						new Dimension(GridSizeMode.Absolute, HitObjectComposer.TOOLBOX_CONTRACTED_SIZE_RIGHT),
					};
					resized = true;
					break;
				}
			}
			if (!resized) return;

			PlaybackPitchControl header = new PlaybackPitchControl();
			flow.Remove(label, true);
			flow.Add(header);
			flow.SetLayoutPosition(header, 0);
			// Keep the native binding alive for its timing-mode reset/disable logic,
			// but replace its visible tabs with the shared continuous control.
			flow.Remove(tabs, false);
			tabs.Hide();
			playback.Add(tabs);
			PlaybackRateControl slider = new PlaybackRateControl();
			// The native tempo must remain at 1: binding it to values below 5%
			// would fail before a compensating adjustment could be applied.
			tabs.Current.BindDisabledChanged(disabled =>
			{
				// Preserve the session speed when switching to Timing.
				slider.Current.Disabled = disabled;
			}, true);
			flow.Add(slider);
			flow.SetLayoutPosition(slider, 1);
			// Let the header define the content width; the slider and value fill exactly it.
			flow.RelativeSizeAxes = Axes.None;
			flow.AutoSizeAxes = Axes.Both;
			flow.Anchor = Anchor.CentreLeft;
			flow.Origin = Anchor.CentreLeft;
			playback.Add(new ShredEditorPitch(editor.Dependencies.Get<EditorClock>(), slider.Current, header));
		}

		public static bool IsShredEditor(Editor editor)
			=> editor.Dependencies.Get<EditorBeatmap>().BeatmapInfo.Ruleset.ShortName == "shred";

		protected override void LoadComplete()
		{
			base.LoadComplete();
			clock.AudioAdjustments.AddAdjustment(AdjustableProperty.Tempo, tempo);
			clock.AudioAdjustments.AddAdjustment(AdjustableProperty.Frequency, frequency);
			speed.ValueChanged += speedChanged;
			header.Pitched.ValueChanged += pitchChanged;
			updateRate();
		}

		private void speedChanged(ValueChangedEvent<double> change) => updateRate();
		private void pitchChanged(ValueChangedEvent<bool> change) => updateRate();
		private void updateRate()
		{
			PlaybackRateControl.GetAudioRate(speed.Value, header.Pitched.Value, out double targetTempo, out double targetFrequency);
			tempo.Value = targetTempo;
			frequency.Value = targetFrequency;
		}

		protected override void Dispose(bool isDisposing)
		{
			if (isDisposing)
			{
				speed.ValueChanged -= speedChanged;
				header.Pitched.ValueChanged -= pitchChanged;
				clock.AudioAdjustments.RemoveAdjustment(AdjustableProperty.Tempo, tempo);
				clock.AudioAdjustments.RemoveAdjustment(AdjustableProperty.Frequency, frequency);
			}
			base.Dispose(isDisposing);
		}
	}
}
#endif
