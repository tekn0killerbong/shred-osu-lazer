using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Tools;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
namespace osu.Game.Rulesets.Shred.Edit
{
	public enum ShredEditorAction
	{
		Pick,
		HammerPull,
		[Category("Shred"), Description("Empty step 1")]
		EmptyStep,
		[Category("Shred"), Description("Empty step 2")]
		EmptyStepAlternate,
		[Category("Shred"), Description("Cycle grid spacing")]
		CycleGridSpacing = 8,
		[Category("Shred"), Description("Cycle grid type")]
		CycleGridType = 9,
		[Category("Shred"), Description("Toggle step recorder")]
		ToggleStepRecorder = 10,
        [Category("Shred"), Description("Decrease grid spacing")]
        DecreaseGridSpacing = 11,
        [Category("Shred"), Description("Increase grid spacing")]
        IncreaseGridSpacing = 12,
        [Category("Shred"), Description("Decrease grid type")]
        DecreaseGridType = 13,
        [Category("Shred"), Description("Increase grid type")]
        IncreaseGridType = 14
	}
	[Cached]
	public partial class ShredHitObjectComposer : HitObjectComposer<ShredHitObject, ShredEditorAction>
	{
		private readonly RulesetInfo recorderRuleset;
		public ShredHitObjectComposer(Ruleset ruleset) : base(ruleset) { recorderRuleset = ruleset.RulesetInfo; }
		public override Bindable<TernaryState> SelectionNewComboState => null;
		public ShredEditorPlayfield Grid => (ShredEditorPlayfield)Playfield;
		protected override DrawableRuleset<ShredHitObject> CreateDrawableRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod> mods) => new DrawableShredEditorRuleset(ruleset, beatmap, mods);
		protected override ComposeBlueprintContainer CreateBlueprintContainer() => new ShredBlueprintContainer(this);
		protected override IReadOnlyList<CompositionTool<ShredEditorAction>> CompositionTools => new CompositionTool<ShredEditorAction>[]
		{
			new ShredCompositionTool("Pick", ShredPracticeArticulation.Pick),
			new ShredCompositionTool("Hammer-pull", ShredPracticeArticulation.HammerPull)
		};
		public ShredStepRecorder StepRecorder { get; private set; }
		public ShredEditorPlayback Playback { get; private set; }
		protected override void LoadComplete()
		{
			base.LoadComplete();
			AddInternal(Playback = new ShredEditorPlayback());
			AddInternal(new ShredRecorderCommandContainer(recorderRuleset)
            {
                RelativeSizeAxes = Axes.Both,
                Depth = -100,
                Child = StepRecorder = new ShredStepRecorder(recorderRuleset)
            });
			Playback.RecorderEnabled.BindTo(StepRecorder.Enabled);
			var recorderToggle = new ShredStepRecorderToggle(StepRecorder.Enabled, Playback.ReturnOnPause, StepRecorder.LongNoteThreshold);
			LeftToolbox.Add(recorderToggle);
			// Fill-flow layout order is independent of draw depth.
			if (recorderToggle.Parent is FillFlowContainer flow)
				flow.SetLayoutPosition(recorderToggle, float.MinValue);
		}
        private ShredHitObject placementChange;
        public void BeginPlacementChange(ShredHitObject note)
        {
            EditorBeatmap.BeginChange();
            placementChange = note;
            TrimSustainsAt(note);
        }
        public void EndPlacementChange()
        {
            ResolveTailConflicts(new[] { placementChange });
            placementChange = null;
            EditorBeatmap.EndChange();
        }
        public void ResolveTailConflicts(IEnumerable<ShredHitObject> changed) => ShredEditorOperations.ResolveTailConflicts(
            EditorBeatmap.HitObjects.OfType<ShredHitObject>(), Grid.PreviousStep, note =>
            {
                note.ApplyDefaults(EditorBeatmap.ControlPointInfo, EditorBeatmap.Difficulty);
                EditorBeatmap.Update(note);
            }, changed);

        // Future time is upstream: retain the old head and cut its extension at the new head.
        public void TrimSustainsAt(ShredHitObject note)
        {
            foreach (var previous in EditorBeatmap.HitObjects.OfType<ShredHitObject>()
                .Where(n => n != note && n.IsWham == note.IsWham && (note.IsWham || n.Column == note.Column)
                    && n.StartTime < note.StartTime - 0.5 && n.EndTime > note.StartTime).ToArray())
            {
                previous.Duration = note.StartTime - previous.StartTime;
                previous.ApplyDefaults(EditorBeatmap.ControlPointInfo, EditorBeatmap.Difficulty);
                EditorBeatmap.Update(previous);
            }
        }
		public double SnapTime(double time) => BeatSnapProvider.SnapTime(time);
		public SnapResult Snap(Vector2 screen, bool wham = false)
		{
			if (wham)
			{
				if (!Grid.ContainsScreen(screen)) return new SnapResult(screen, null);
				double whamTime = SnapTime(Grid.TimeAt(screen, 12));
				return new SnapResult(Grid.ToScreenSpace(Grid.PositionAt(12, whamTime)), whamTime, Grid);
			}
			if (!Grid.InGrid(screen)) return new SnapResult(screen, null);
			double time = BeatSnapProvider.SnapTime(Grid.TimeAt(screen));
			return new SnapResult(Grid.ToScreenSpace(Grid.PositionAt(Grid.ColumnAt(screen), time)), time, Grid);
		}
	}
	public class ShredCompositionTool : CompositionTool<ShredEditorAction>
	{
		private readonly ShredPracticeArticulation articulation;
		public ShredCompositionTool(string name, ShredPracticeArticulation articulation) : base(name) { this.articulation = articulation; Action = ShredEditorAction.Pick; if (articulation == ShredPracticeArticulation.HammerPull) Action = ShredEditorAction.HammerPull; }
		public override Drawable CreateIcon() => new SpriteText { Text = Name.ToString().Substring(0, 1), Font = FontUsage.Default.With(size: 22) };
		public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new ShredPlacementBlueprint(articulation);
	}
    public partial class ShredPlacementBlueprint : HitObjectPlacementBlueprint
    {
        [Resolved] private ShredHitObjectComposer composer { get; set; }
        private readonly Box preview = new Box { Origin = Anchor.Centre, Alpha = 0.2f };
        private readonly Box extension = new Box { Origin = Anchor.Centre, Alpha = 0.12f };
        private readonly Circle whamJoint = new Circle { Origin = Anchor.Centre, Alpha = 0 };
        private readonly Box whamHook = new Box { Origin = Anchor.CentreLeft, Rotation = 20, Alpha = 0 };
        private readonly ShredPracticeArticulation toolArticulation;
        private bool inGrid;
        private bool dragging;
        private Vector2 downScreen;
        private double start;
        private double appliedDuration = -1;
        private int column;
        private ShredHitObject Note => (ShredHitObject)HitObject;
        public ShredPlacementBlueprint(ShredPracticeArticulation articulation) : base(new ShredHitObject { Articulation = articulation }) { toolArticulation = articulation; }
        [BackgroundDependencyLoader] private void load() { Add(whamHook); Add(extension); Add(whamJoint); Add(preview); }
        protected override bool IsValidForPlacement => inGrid && base.IsValidForPlacement
            && (!Note.IsWham || Note.IsLong);
        public override bool ReplacesExistingObject(HitObject existing)
            => existing is ShredHitObject other && other.IsWham == Note.IsWham && other.Row == Note.Row && other.Fret == Note.Fret && base.ReplacesExistingObject(existing);
        public override SnapResult UpdateTimeAndPosition(Vector2 screen, double referenceTime)
        {
            SnapResult result;
            if (PlacementActive == PlacementState.Active)
            {
                dragging |= (screen - downScreen).LengthSquared >= 9;
                if (dragging && (Note.Fret != 0 || Note.IsWham)) composer.Grid.ScrollAtEdge(screen, column, Time.Elapsed);
                double end = Math.Max(0, composer.SnapTime(composer.Grid.TimeAt(screen, column)));
                if (dragging && (Note.Fret != 0 || Note.IsWham))
                {
                    Note.StartTime = Math.Max(0, Math.Min(start, end));
                    Note.Duration = Math.Abs(end - start);
                    if (Note.IsWham) Note.Duration = Math.Max(composer.Grid.MinimumDuration(Note.StartTime), Note.Duration);
                    RefreshDuration();
                }
                result = new SnapResult(composer.Grid.ToScreenSpace(composer.Grid.PositionAt(column, Note.StartTime)), Note.StartTime, composer.Grid);
            }
            else
            {
                result = composer.Snap(screen);
                inGrid = result.Time.HasValue;
                if (!inGrid) { preview.Alpha = extension.Alpha = whamHook.Alpha = whamJoint.Alpha = 0; return result; }
                setColumn(composer.Grid.ColumnAt(screen));
                base.UpdateTimeAndPosition(result.ScreenSpacePosition, result.Time.Value);
                start = Note.StartTime;
                Note.Duration = Note.IsWham ? composer.Grid.MinimumDuration(start) : 0;
                RefreshDuration();
            }
            preview.Position = ToLocalSpace(result.ScreenSpacePosition);
            Vector2 size = Note.IsWham ? composer.Grid.WhamSize : composer.Grid.NoteSize(Note.Fret);
            // Convert both positions and sizes through screen space so zoomed
            // playfield containers cannot shrink the editing hit area.
            Vector2 sizeEnd = ToLocalSpace(composer.Grid.ToScreenSpace(composer.Grid.PositionAt(column, Note.StartTime) + size));
            preview.Size = sizeEnd - preview.Position;
            preview.Shear = new Vector2(composer.Grid.Skin.Shear(Note.Row), 0);
            preview.Colour = Note.IsWham ? ShredWhamGeometry.Colour : Note.Fret == 0 ? Color4.White : ShredPlayfield.FretColour(Note.Fret);
            preview.Alpha = 0.2f;
            Vector2 tailPosition = composer.Grid.PositionAt(column, Note.EndTime);
            if (Note.IsLong && !Note.IsWham)
                tailPosition = ShredNoteVisual.SustainEndAtUpstreamEdge(tailPosition, composer.Grid.Skin.Direction(Note.Row), size.Y);
            Vector2 tail = ToLocalSpace(composer.Grid.ToScreenSpace(tailPosition));
            extension.Position = (preview.Position + tail) / 2;
            extension.Size = Note.IsWham ? new Vector2(Math.Abs(tail.X - preview.X), preview.Height) : new Vector2(preview.Width, Math.Abs(tail.Y - preview.Y));
            extension.Shear = preview.Shear;
            extension.Colour = preview.Colour;
            extension.Alpha = Note.IsLong ? 0.12f : 0;
            whamHook.Alpha = whamJoint.Alpha = 0;
            if (Note.IsWham)
            {
                // Opaque bar with a round, seamless bend and no centred head.
                preview.Alpha = 0;
                extension.Alpha = 0.2f;
                extension.Shear = Vector2.Zero;
                whamHook.Alpha = 0.2f;
                whamHook.Position = tail - new Vector2(ShredWhamGeometry.HookInset * composer.Grid.Unit, 0);
                whamHook.Size = new Vector2(preview.Width * 3, preview.Height);
                whamJoint.Alpha = 0.2f;
                whamJoint.Position = whamHook.Position;
                whamJoint.Size = new Vector2(preview.Height);
                whamJoint.Colour = ShredWhamGeometry.Colour;
                whamHook.Colour = ShredWhamGeometry.Colour;
            }
            return result;
        }
		private void setColumn(int value)
		{
			column = value;
			Note.Articulation = toolArticulation;
			if (column == 12)
			{
				Note.Articulation = ShredPracticeArticulation.Wham;
				Note.Row = 0;
				Note.Fret = 0;
			}
			else
			{
				Note.X = column;
				// Preserve the selected articulation for OPEN pull-offs.
			}
		}
        private void RefreshDuration()
        {
            if (appliedDuration == Note.Duration) return;
            appliedDuration = Note.Duration;
            ApplyDefaultsToHitObject();
        }
        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left || !inGrid) return false;
            downScreen = e.ScreenSpaceMousePosition;
            BeginPlacement(true);
            return true;
        }
        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button != MouseButton.Left || PlacementActive != PlacementState.Active) return false;
            dragging = true;
            return true;
        }
        protected override void OnMouseUp(MouseUpEvent e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButton.Left && PlacementActive == PlacementState.Active) EndPlacement(true);
        }
        public override void EndPlacement(bool commit)
        {
            if (PlacementActive == PlacementState.Finished) return;
            double viewTime = EditorClock.CurrentTime;
            bool changing = commit && IsValidForPlacement;
            if (changing) composer.BeginPlacementChange(Note);
            try { base.EndPlacement(commit); }
            finally { if (changing) composer.EndPlacementChange(); }
            // Cancel lazer's generic auto-seek after creation; only explicit
            // timeline navigation and edge dragging may move this view.
            EditorClock.Seek(viewTime);
        }
    }
	public partial class ShredBlueprintContainer : ComposeBlueprintContainer
	{
		private readonly ShredHitObjectComposer composer;
		private double selectionViewTime;
        private double selectionSampleViewTime;
		private double selectionStartTime;
		private double selectionEndTime;
		private Vector2 selectionStartPoint;
		private Vector2 selectionEndPoint;
		private int selectionEndRow;
		private Vector2 dragOriginMouse;
		private int dragOriginRow;
		private int dragOriginFret;
		private double dragOriginTime;
		private bool dragWham;
		private readonly HashSet<ShredHitObject> movedNotes = new HashSet<ShredHitObject>();
		private bool selecting;
        private HashSet<HitObject> additiveSelection = new HashSet<HitObject>();
		private ShredHitObject resizingNote;
		private double resizeOriginalEnd;
		private double resizeGrabTime;
		private int resizeColumn;
		private Vector2 resizeCursor;
		private readonly Dictionary<ShredHitObject, (double time, double end, int column)> clippedDragOrigins
			= new Dictionary<ShredHitObject, (double time, double end, int column)>();
		private Vector2 selectionCursor;
		private int selectionColumn;
		public ShredBlueprintContainer(ShredHitObjectComposer composer) : base(composer) { this.composer = composer; }
		protected override SelectionHandler<HitObject> CreateSelectionHandler() => new ShredSelectionHandler();
		protected override bool OnMouseDown(MouseDownEvent e)
		{
            if (e.Button == MouseButton.Left && composer.Grid.ContainsScreen(e.ScreenSpaceMousePosition))
                composer.Grid.FocusRow(composer.Grid.RowAt(e.ScreenSpaceMousePosition));
            if (handleRangeSelection(e)) return true;
			if (e.Button == MouseButton.Right && !e.ControlPressed && !e.ShiftPressed && !e.AltPressed && !e.SuperPressed)
			{
				var target = SelectionBlueprints.AliveChildren.OfType<ShredSelectionBlueprint>()
					.Where(b => b.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
					.OrderByDescending(b => b.HitObject.StartTime).FirstOrDefault();
				if (target != null && !target.IsSelected)
				{
					Beatmap.BeginChange();
					Beatmap.Remove(target.HitObject);
					Beatmap.EndChange();
					return true;
				}
			}
			return base.OnMouseDown(e);
		}
		protected override bool OnDragStart(DragStartEvent e)
		{
			if (modifierClick) return false;
			movedNotes.Clear();
			deselectOnClick = null;
			selecting = false;
			clippedDragOrigins.Clear();
			dragOriginMouse = e.ScreenSpaceMouseDownPosition;
			dragOriginRow = composer.Grid.RowAt(dragOriginMouse);
			dragWham = composer.Grid.IsWhamLane(dragOriginMouse);
			dragOriginFret = composer.Grid.DragColumnAt(dragOriginMouse, dragOriginRow) % 6;
			int originColumn = dragOriginRow * 6;
			if (dragWham) originColumn = 12;
			dragOriginTime = composer.Grid.TimeAt(dragOriginMouse, originColumn);
			if (e.Button == MouseButton.Left && CurrentPlacement == null)
			{
				// A selected LN tail remains reachable under a newer short head.
				var target = SelectionBlueprints.AliveChildren.OfType<ShredSelectionBlueprint>()
					.Where(b => b.ReceivePositionalInputAt(e.ScreenSpaceMouseDownPosition))
					.OrderByDescending(b => b.IsSelected && composer.Grid.EndHandleContains(b.HitObject, e.ScreenSpaceMouseDownPosition))
                    .ThenByDescending(b => b.HitObject.StartTime).FirstOrDefault();
				if (target != null && composer.Grid.EndHandleContains(target.HitObject, e.ScreenSpaceMouseDownPosition))
				{
					resizingNote = target.HitObject;
					resizeColumn = resizingNote.Column;
					if (resizingNote.IsWham) resizeColumn = 12;
					resizeOriginalEnd = resizingNote.EndTime;
					resizeGrabTime = composer.Grid.TimeAt(e.ScreenSpaceMouseDownPosition, resizeColumn);
					resizeCursor = e.ScreenSpaceMousePosition;
					Beatmap.BeginChange();
					updateResize();
					return true;
				}
			}
			// Snapshot true heads BEFORE the framework applies the first movement.
			foreach (var blueprint in SelectionBlueprints.AliveChildren.OfType<ShredSelectionBlueprint>())
			{
				ShredHitObject note = blueprint.HitObject;
				int column = note.Column;
				if (note.IsWham) column = 12;
				clippedDragOrigins[note] = (note.StartTime, note.EndTime, column);
			}
			additiveSelection = e.ControlPressed ? new HashSet<HitObject>(SelectedItems) : new HashSet<HitObject>();
			bool accepted = base.OnDragStart(e);
			if (accepted && CurrentPlacement == null && DragBox.State == Visibility.Visible)
			{
				selecting = true;
				selectionViewTime = composer.Grid.CurrentTime;
				selectionStartPoint = composer.Grid.ToLocalSpace(e.ScreenSpaceMouseDownPosition);
				selectionStartTime = composer.Grid.TimeAt(e.ScreenSpaceMouseDownPosition, dragOriginRow * 6);
				selectionCursor = e.ScreenSpaceMousePosition;
				selectionColumn = composer.Grid.ColumnAt(e.ScreenSpaceMouseDownPosition);
				if (selectionColumn < 0)
				{
					selectionColumn = 0;
					if (composer.Grid.ToLocalSpace(e.ScreenSpaceMouseDownPosition).Y >= composer.Grid.DrawHeight / 2) selectionColumn = 6;
				}
				captureSelectionEnd();
			}
			return accepted;
		}
		protected override void OnDrag(DragEvent e)
		{
			if (resizingNote != null)
			{
				resizeCursor = e.ScreenSpaceMousePosition;
				updateResize();
				return;
			}
			selectionCursor = e.ScreenSpaceMousePosition;
			if (selecting) captureSelectionEnd();
			base.OnDrag(e);
		}
		protected override void OnDragEnd(DragEndEvent e)
		{
			if (resizingNote != null)
			{
				finishResize();
				return;
			}
			base.OnDragEnd(e);
			clippedDragOrigins.Clear();
			selecting = false;
			movedNotes.Clear();
		}
		protected override void Update()
		{
			if (resizingNote != null && composer.Grid.ScrollAtEdge(resizeCursor, resizeColumn, Time.Elapsed))
				updateResize();
			if (selecting && CurrentPlacement == null && DragBox.State == Visibility.Visible)
			{
				if (composer.Grid.ScrollAtEdge(selectionCursor, selectionColumn, Time.Elapsed))
                {
                    captureSelectionEnd();
                    UpdateSelectionFromDragBox(additiveSelection);
                }
			}
			base.Update();
		}
		private void updateResize()
		{
			double delta = composer.Grid.TimeAt(resizeCursor, resizeColumn) - resizeGrabTime;
			double end = composer.SnapTime(resizeOriginalEnd + delta);
			double minimumEnd = resizingNote.StartTime + composer.Grid.MinimumDuration(resizingNote.StartTime);
			double duration = Math.Max(minimumEnd, end) - resizingNote.StartTime;
			if (Math.Abs(resizingNote.Duration - duration) < 0.001) return;
			resizingNote.Duration = duration;
			resizingNote.ApplyDefaults(Beatmap.ControlPointInfo, Beatmap.Difficulty);
			Beatmap.Update(resizingNote);
		}
		private void finishResize()
		{
			composer.ResolveTailConflicts(new[] { resizingNote });
			resizingNote = null;
			Beatmap.EndChange();
		}
		protected override void Dispose(bool isDisposing)
		{
			if (isDisposing && resizingNote != null) finishResize();
			base.Dispose(isDisposing);
		}
		private void captureSelectionEnd()
		{
			selectionSampleViewTime = composer.Grid.CurrentTime;
			selectionEndRow = composer.Grid.RowAt(selectionCursor);
			selectionEndTime = composer.Grid.TimeAt(selectionCursor, selectionEndRow * 6);
			selectionEndPoint = composer.Grid.ToLocalSpace(selectionCursor);
		}
		protected override void UpdateSelectionFromDragBox(HashSet<HitObject> selectionBeforeDrag)
		{
			if (!selecting) return;
			float left = Math.Min(selectionStartPoint.X, selectionEndPoint.X);
			float right = Math.Max(selectionStartPoint.X, selectionEndPoint.X);
			foreach (var blueprint in SelectionBlueprints.Children.OfType<ShredSelectionBlueprint>().ToArray())
			{
				ShredHitObject note = blueprint.HitObject;
				bool included = selectionBeforeDrag.Contains(note);
				if (!note.IsWham)
				{
					bool rowIncluded = note.Row == dragOriginRow || selectionEndRow != dragOriginRow;
                    var range = ShredMarquee.Range(note.Row, dragOriginRow, selectionEndRow,
                        selectionStartTime, selectionEndTime, selectionViewTime, selectionSampleViewTime);
                    included |= rowIncluded && ShredMarquee.Intersects(note.StartTime, note.EndTime,
                        range.from, range.to, composer.Grid.Receiver(note.Row, note.Fret).X,
                        composer.Grid.Travel(note.Row).X, composer.Grid.VisibleTime,
                        selectionViewTime, selectionSampleViewTime, left, right, composer.Grid.NoteSize(note.Fret).X / 2);
				}
				else
				{
					Vector2 head = composer.Grid.WhamReceiver + new Vector2(composer.Grid.WhamTravel * (float)((note.StartTime - selectionSampleViewTime) / composer.Grid.VisibleTime), 0);
					included |= head.X >= left && head.X <= right
						&& head.Y >= Math.Min(selectionStartPoint.Y, selectionEndPoint.Y) && head.Y <= Math.Max(selectionStartPoint.Y, selectionEndPoint.Y);
				}
				if (included) blueprint.Select();
				else blueprint.Deselect();
			}
		}
		protected override void DragOperationCompleted()
		{
			// Resolve only when dropped, so dragging across a note does not erase it.
			foreach (ShredHitObject moved in movedNotes.ToArray())
				foreach (ShredHitObject old in Beatmap.HitObjects.OfType<ShredHitObject>()
					.Where(n => n != moved && !movedNotes.Contains(n) && ShredEditorOperations.Conflicts(n, moved)).ToArray()) Beatmap.Remove(old);
			var kept = new List<ShredHitObject>();
			foreach (ShredHitObject note in movedNotes.ToArray())
			{
				if (kept.Any(n => n.IsWham == note.IsWham && (note.IsWham || n.Column == note.Column) && Math.Abs(n.StartTime - note.StartTime) < 0.5)) Beatmap.Remove(note);
				else kept.Add(note);
			}
			composer.ResolveTailConflicts(kept);
			base.DragOperationCompleted();
		}

		public override HitObjectSelectionBlueprint CreateHitObjectBlueprintFor(HitObject hitObject) => new ShredSelectionBlueprint((ShredHitObject)hitObject, composer.Grid);
		protected override bool TryMoveBlueprints(DragEvent e, IList<(SelectionBlueprint<HitObject> blueprint, Vector2[] originalSnapPositions)> blueprints)
		{
			int targetRow = composer.Grid.RowAt(e.ScreenSpaceMousePosition);
			bool flip = targetRow != dragOriginRow;
			int targetColumn = targetRow * 6;
			if (dragWham) targetColumn = 12;
			double targetTime = composer.Grid.TimeAt(e.ScreenSpaceMousePosition, targetColumn);
			// Snap each moved head to the active subdivision.
			
			int targetFret = composer.Grid.DragColumnAt(e.ScreenSpaceMousePosition, targetRow) % 6;
			int fretDelta = targetFret - dragOriginFret;
			var moves = new List<(ShredHitObject note, double time, int column)>();
			foreach (var entry in blueprints)
			{
				ShredHitObject note = (ShredHitObject)entry.blueprint.Item;
				if (!clippedDragOrigins.TryGetValue(note, out var origin)) return false;
				double time = ShredEditorOperations.DragTime(origin.time, dragOriginTime, targetTime, composer.SnapTime);
				int column = origin.column;
				if (!note.IsWham)
				{
					int row = origin.column / 6;
					if (flip) row = 1 - row;
					int fret = origin.column % 6;
					if (fret > 0) fret += fretDelta;
					if (fret < 0 || fret > 5 || (origin.column % 6 != 0 && fret == 0)) return false;
					column = row * 6 + fret;
				}
				if (time < 0) return false;
				moves.Add((note, time, column));
			}
			foreach (var move in moves)
			{
				if (clippedDragOrigins.TryGetValue(move.note, out var original) && original.end > original.time)
                    move.note.Duration = ShredEditorOperations.DragDuration(original.time, original.end);
                move.note.StartTime = move.time;
				if (!move.note.IsWham) move.note.X = move.column;
				movedNotes.Add(move.note);
				Beatmap.Update(move.note);
			}
			composer.Grid.FocusRow(targetRow);
			return true;
		}
	}
	public partial class ShredSelectionHandler : EditorSelectionHandler
	{
		public void InvertSelection()
		{
			EditorBeatmap.BeginChange();
			ShredEditorOperations.InvertTypes(SelectedBlueprints.Select(b => (ShredHitObject)b.Item), n => EditorBeatmap.Update(n));
			EditorBeatmap.EndChange();
		}
		public override bool HandleFlip(Direction direction, bool flipOverOrigin)
			=> ShredEditorOperations.Flip(SelectedBlueprints.Select(b => (ShredHitObject)b.Item),
				direction == Direction.Horizontal, flipOverOrigin, note => EditorBeatmap.Update(note));

		protected override IEnumerable<MenuItem> GetContextMenuItemsForSelection(IEnumerable<SelectionBlueprint<HitObject>> selection)
		{
			ShredHitObject[] notes = selection.Select(s => (ShredHitObject)s.Item).ToArray();
			var invert = new OsuMenuItem("Invert all", MenuItemType.Standard, InvertSelection);
			if (notes.Where(n => !n.IsWham && n.Fret > 0).Select(n => n.Articulation).Distinct().Count() <= 1)
			{
				yield return new OsuMenuItem("Convert type", MenuItemType.Standard, InvertSelection);
				foreach (MenuItem item in base.GetContextMenuItemsForSelection(selection)) yield return item;
				yield break;
			}
			var conversions = new List<MenuItem> { invert };
			foreach (ShredPracticeArticulation type in new[] { ShredPracticeArticulation.Pick, ShredPracticeArticulation.HammerPull })
			{
				string label = "Pick";
				if (type == ShredPracticeArticulation.HammerPull) label = "Hammer-pull";
				conversions.Add(new OsuMenuItem(label, MenuItemType.Standard, () =>
				{
					EditorBeatmap.BeginChange();
					ShredEditorOperations.ConvertType(notes, type, note =>
					{
						note.ApplyDefaults(EditorBeatmap.ControlPointInfo, EditorBeatmap.Difficulty);
						EditorBeatmap.Update(note);
					});
					EditorBeatmap.EndChange();
				}));
			}
			yield return new OsuMenuItem("Convert type") { Items = conversions.ToArray() };
			foreach (MenuItem item in base.GetContextMenuItemsForSelection(selection)) yield return item;
		}
	}
	public partial class ShredSelectionBlueprint : HitObjectSelectionBlueprint<ShredHitObject>
	{
		private readonly ShredEditorPlayfield field;
		public ShredSelectionBlueprint(ShredHitObject note, ShredEditorPlayfield field) : base(note)
		{
			this.field = field ?? throw new ArgumentNullException(nameof(field));
		}
		public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
		{
			if (field == null || DrawableObject == null || !field.ContainsScreen(screenSpacePos)) return false;
			return field.NoteContains(HitObject, screenSpacePos);
		}
		protected override void Update()
		{
			base.Update();
			updateVisualSelection();
		}
		protected override void OnSelected()
		{
			base.OnSelected();
			updateVisualSelection();
		}
		protected override void OnDeselected()
		{
			base.OnDeselected();
			updateVisualSelection();
		}
		private void updateVisualSelection()
		{
			if (DrawableObject is DrawableShredEditorNote note)
				note.EditorSelected = State == SelectionState.Selected;
		}
	}
}
