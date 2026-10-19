using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Shred.Edit
{
    /// <summary>Separates the return anchor from the last actual playback stop.</summary>
    public partial class ShredEditorPlayback : Component
    {
        [Resolved] private EditorClock clock { get; set; }
        public readonly BindableBool ReturnOnPause = new BindableBool(false);
        public readonly BindableBool RecorderEnabled = new BindableBool();
        private double playbackStart;
        private double lastStoppedPosition;
        private double pausePosition;
        private bool hasStart;
        private bool hasPause;
        private bool wasRunning;
        private bool internalSeek;
        private bool atEnd => clock.TrackLength > 0 && clock.CurrentTime >= clock.TrackLength - 0.001;

        private bool navigationPending;
        private bool navigationRequiresPlayback;
        public void NotifyNavigation() => scheduleNavigation(false);
        private void scheduleNavigation(bool requiresPlayback)
        {
            if (navigationPending)
            {
                navigationRequiresPlayback &= requiresPlayback;
                return;
            }
            navigationPending = true;
            navigationRequiresPlayback = requiresPlayback;
            Scheduler.Add(() =>
            {
                if (!navigationPending) return;
                navigationPending = false;
                if (!navigationRequiresPlayback || clock.IsRunning) captureNavigation();
            });
        }
        public void NotifyNavigation(double position)
        {
            navigationPending = false;
            playbackStart = Math.Clamp(position, 0, clock.TrackLength);
            hasStart = true;
            if (!clock.IsRunning) lastStoppedPosition = playbackStart;
        }
        private void captureNavigation() => NotifyNavigation(clock.CurrentTimeAccurate);

        protected override void LoadComplete()
        {
            base.LoadComplete();
            // SeekSmoothlyTo raises this before IsSeeking becomes true, and
            // Stop raises it before stopping playback. Capture after the input
            // finishes, while excluding a normal pause from navigation.
            clock.SeekingOrStopped.BindValueChanged(value =>
            {
                if (!internalSeek && value.NewValue && clock.IsRunning) scheduleNavigation(true);
            });
            lastStoppedPosition = clock.CurrentTime;
            wasRunning = clock.IsRunning;
            if (wasRunning) NotifyNavigation(clock.CurrentTime);
        }

        public void Toggle(bool returnToStart, bool resumeStopped = false)
        {
            // A navigation followed by Space in the same input frame must use
            // its final seek destination before applying Return on pause.
            if (navigationPending && (!navigationRequiresPlayback || clock.IsRunning)) captureNavigation();
            if (returnToStart && atEnd)
            {
                clock.Stop();
                seek(hasStart && playbackStart < clock.TrackLength ? playbackStart : 0);
                start();
                return;
            }
            if (clock.IsRunning)
            {
                pausePosition = clock.CurrentTimeAccurate;
                hasPause = true;
                wasRunning = false;
                clock.Stop();
                if (returnToStart && hasStart) seek(playbackStart);
                lastStoppedPosition = clock.CurrentTimeAccurate;
                return;
            }
            if (resumeStopped && hasPause) seek(pausePosition);
            start();
        }

        private void seek(double position)
        {
            internalSeek = true;
            try { clock.Seek(Math.Clamp(position, 0, clock.TrackLength)); }
            finally { internalSeek = false; }
        }
        private void start()
        {
            playbackStart = clock.CurrentTimeAccurate;
            lastStoppedPosition = playbackStart;
            hasStart = true;
            // Finish a smooth navigation before Start clears its transform.
            seek(playbackStart);
            clock.Start();
            wasRunning = clock.IsRunning;
        }
        protected override void Update()
        {
            base.Update();
            if (clock.IsRunning && !wasRunning)
            {
                playbackStart = lastStoppedPosition;
                hasStart = true;
            }
            if (!clock.IsRunning && wasRunning && !atEnd)
            {
                pausePosition = clock.CurrentTimeAccurate;
                hasPause = true;
                if (ReturnOnPause.Value && hasStart) seek(playbackStart);
            }
            if (!clock.IsRunning && !atEnd) lastStoppedPosition = clock.CurrentTimeAccurate;
            wasRunning = clock.IsRunning;
        }
    }
}
