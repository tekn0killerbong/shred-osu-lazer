using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Shred.Edit
{
	/// <summary>Remembers the playback origin independently of the moving cursor.</summary>
	public partial class ShredEditorPlayback : Component
	{
		[Resolved] private EditorClock clock { get; set; }
		public readonly BindableBool ReturnOnPause = new BindableBool(true);
		public readonly BindableBool RecorderEnabled = new BindableBool();
		private bool shouldReturn => RecorderEnabled.Value && ReturnOnPause.Value;
        private double playbackStart;
		private double lastStoppedPosition;
		private bool hasStart;
		private bool wasRunning;
		private bool navigated;
		public void NotifyNavigation() { if (clock.IsRunning || wasRunning) navigated = true; }
		private bool atEnd => clock.TrackLength > 0 && clock.CurrentTime >= clock.TrackLength - 0.001;

		protected override void LoadComplete()
		{
			base.LoadComplete();
            // Observe Seek synchronously; an immediate seek may finish before our next Update.
            clock.SeekingOrStopped.BindValueChanged(value =>
            {
                if (value.NewValue && clock.IsSeeking && clock.IsRunning) navigated = true;
            });
			lastStoppedPosition = clock.CurrentTime;
			wasRunning = clock.IsRunning;
			if (wasRunning) { playbackStart = clock.CurrentTime; hasStart = true; }
		}

		public void Toggle(bool returnToStart)
		{
			if (clock.IsSeeking && (clock.IsRunning || wasRunning)) navigated = true;
			returnToStart &= shouldReturn && !navigated;
			// At natural track end, Space starts again from the stored origin,
			// whether the underlying track still reports IsRunning or not.
			if (returnToStart && atEnd)
			{
				clock.Stop();
				double target = 0;
				if (hasStart && playbackStart < clock.TrackLength) target = playbackStart;
				clock.Seek(Math.Clamp(target, 0, clock.TrackLength));
				start();
				return;
			}
			if (clock.IsRunning)
			{
				// Consume this stop before notifying clock listeners. Ctrl+Space
				// must never be interpreted later as a UI pause requesting return.
				wasRunning = false;
				clock.Stop();
				if (returnToStart && hasStart) clock.Seek(Math.Clamp(playbackStart, 0, clock.TrackLength));
				wasRunning = false;
				lastStoppedPosition = clock.CurrentTime;
				return;
			}
			start();
		}

		private void start()
		{
			navigated = false;
			playbackStart = clock.CurrentTimeAccurate;
			hasStart = true;
			clock.Start();
			wasRunning = clock.IsRunning;
		}

		protected override void Update()
		{
			base.Update();
			// Also remember origins when playback was started using the UI.
			if (clock.IsRunning && !wasRunning)
			{
				navigated = false;
				playbackStart = lastStoppedPosition;
				hasStart = true;
			}
			if (clock.IsSeeking && (clock.IsRunning || wasRunning)) navigated = true;
            if (!clock.IsRunning && wasRunning && !atEnd && shouldReturn && hasStart && !navigated)
                clock.Seek(Math.Clamp(playbackStart, 0, clock.TrackLength));
            if (!clock.IsRunning && !atEnd) lastStoppedPosition = clock.CurrentTime;
			wasRunning = clock.IsRunning;
		}
	}
}
