#if SHRED_OFFICIAL
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.Timing;
using osu.Game.Screens.Edit.Timing.Multiselect;

namespace osu.Game.Rulesets.Shred.Compatibility
{
	internal partial class ShredTimingMultiselect : Component
	{
		private readonly Editor editor;
		private bool waiting;
		private ShredTimingMultiselect(Editor editor) => this.editor = editor;

		public static void Install(Editor editor)
		{
			PlaybackControl playback = editor.ChildrenOfType<PlaybackControl>().SingleOrDefault();
			if (playback != null && !playback.ChildrenOfType<ShredTimingMultiselect>().Any())
				playback.Add(new ShredTimingMultiselect(editor));
		}

		protected override void LoadComplete()
		{
			base.LoadComplete();
			editor.Mode.ValueChanged += modeChanged;
			waiting = editor.Mode.Value == EditorScreenMode.Timing;
		}
		private void modeChanged(ValueChangedEvent<EditorScreenMode> change) => waiting = change.NewValue == EditorScreenMode.Timing;

		protected override void Update()
		{
			base.Update();
			if (!waiting) return;
			TimingScreen screen = editor.ChildrenOfType<TimingScreen>().SingleOrDefault();
			if (screen?.MainContent == null) return;
			if (screen.MainContent.ChildrenOfType<MultiTimingContent>().Any()) { waiting = false; return; }
			ControlPointList list = screen.MainContent.ChildrenOfType<ControlPointList>().SingleOrDefault();
			if (list == null || !list.IsLoaded) return;
			// The native screen loads its main content asynchronously after OnLoadComplete.
			// Replace only that content, retaining its timeline, mode and selected group.
			Drawable original = screen.MainContent.Children.SingleOrDefault(c => c.ChildrenOfType<ControlPointList>().Contains(list));
			if (original == null) return;
			var replacement = new MultiTimingContent(screen.Dependencies.Get<EditorBeatmap>(),
				(EditorChangeHandler)screen.Dependencies.Get<IEditorChangeHandler>(), screen.SelectedGroup)
			{
				SelectClosestTimingPoint = list.SelectClosestTimingPoint
			};
			screen.MainContent.Remove(original, true);
			screen.MainContent.Add(replacement);
			waiting = false;
		}

		protected override void Dispose(bool isDisposing)
		{
			if (isDisposing) editor.Mode.ValueChanged -= modeChanged;
			base.Dispose(isDisposing);
		}
	}
}
#endif
