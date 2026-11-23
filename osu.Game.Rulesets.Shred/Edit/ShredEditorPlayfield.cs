using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Rulesets.Shred.Skinning;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Graphics;
namespace osu.Game.Rulesets.Shred.Edit
{
	[Cached]
	public partial class ShredEditorPlayfield : Playfield
	{
		[Resolved(canBeNull: true)] private Editor editor { get; set; }
		[Resolved] private EditorClock editorClock { get; set; }
		[Resolved] private EditorBeatmap beatmap { get; set; }
		[Resolved] private BindableBeatDivisor beatDivisor { get; set; }
		[Resolved] private OsuColour colours { get; set; }
		public ShredSkinSettings Skin { get; } = ShredSkinSettings.Load();
		private readonly ShredGlyph[,] receptors = new ShredGlyph[2, 5];
		private readonly Box[,] lanes = new Box[2, 5];
		private readonly Box[] stringLines = new Box[2];
		private readonly SpriteText[] openLabels = new SpriteText[2];
		private const int max_grid_lines = 512;
		private readonly Box[,] gridLines = new Box[3, max_grid_lines];
        private readonly int[] gridLineCounts = new int[3];
		private readonly Box[] cursors = new Box[2];
		private readonly Box whamLine = new Box { Origin = Anchor.Centre, Colour = ShredWhamGeometry.Colour };
		private readonly Box[] activeRows = { new Box(), new Box() };
		public int ActiveRow { get; private set; }
		public void FocusRow(int row) { ActiveRow = Math.Clamp(row, 0, 1); }
		public int RowAt(Vector2 screen)
		{
			if (ToLocalSpace(screen).Y >= DrawHeight / 2) return 1;
			return 0;
		}
		private readonly Container grid = new Container { RelativeSizeAxes = Axes.Both };
		// The native editor zoom uses a fixed reference, independent of the gameplay preference.
		public double PlaybackRate => ShredScrollSpeed.ValidPlaybackRate(
			editorClock.AudioAdjustments.AggregateTempo.Value * editorClock.AudioAdjustments.AggregateFrequency.Value);
		public double VisibleTime => ShredScrollSpeed.MapDuration(ShredEditorOperations.VisibleTime(beatmap.TimelineZoom), PlaybackRate);

		// Use the full screen's gameplay dimensions, expressed in this viewport's
        // coordinates. Sidebar/timeline space must not change the size of the frets.
        public float MovementUnit
        {
            get
            {
                Vector2 size = editor == null ? DrawSize
                    : ToLocalSpace(editor.ToScreenSpace(editor.DrawSize)) - ToLocalSpace(editor.ToScreenSpace(Vector2.Zero));
                return Math.Min(size.X / 1000, size.Y / 720);
            }
        }
		public float Unit => MovementUnit * ShredPlayfieldGeometry.VisualScale;
		public float ColumnWidth => Skin.ColumnWidth[0] * Unit;
		public double MinimumDuration(double time) => beatmap.ControlPointInfo.TimingPointAt(time).BeatLength / beatDivisor.Value;
		public double PreviousStep(double position) => ShredGridTicks.Previous(beatmap.ControlPointInfo, beatDivisor.Value, position);
		public double NextStep(double position) => ShredGridTicks.Next(beatmap.ControlPointInfo, beatDivisor.Value, position);
		public double SnapTime(double position) => Beatmaps.ShredGridTiming.Snap(beatmap.ControlPointInfo, beatDivisor.Value, position);
		public double CurrentTime => editorClock.CurrentTime;
		// Visual passage is a timestamp comparison, not the 1 ms chord grouping
		// tolerance. Keep the exact receiver position visible for every head.
		public bool HasPassed(double time) => CurrentTime > time + 0.000002;
        private double visibleLeadTime = double.MaxValue;
        public double LastVisibleTime => CurrentTime + visibleLeadTime;
		private float centreShift => Skin.OpenCentre + Skin.EffectiveRowOffsetBottom / 2;

		public Vector2 Receiver(int row, int fret)
		{
			float x = fret == 0 ? Skin.OpenCentre : Skin.FretCentre(fret);
			if (row == 1) x += Skin.EffectiveRowOffsetBottom;
			return new Vector2(DrawWidth / 2 + (x - centreShift) * Unit,
				DrawHeight / 2 + (-Skin.RowGap / 2 + row * Skin.RowGap) * Unit);
		}
		public Vector2 NoteSize(int fret)
		{
			if (fret == 0) return new Vector2(Skin.OpenWidth, 8) * Unit;
			return new Vector2(Skin.ColumnWidth[fret - 1], Skin.NoteHeight) * Unit;
		}
		protected override HitObjectContainer CreateHitObjectContainer() => new ShredHitObjectContainer();
		public void SetFretPressed(int row, int fret, bool pressed)
		{
			receptors[row, fret]?.SetPressed(pressed);
		}
		public void ResetFretPresses()
		{
			for (int row = 0; row < 2; row++)
				for (int fret = 0; fret < 5; fret++) SetFretPressed(row, fret, false);
		}
		// Equal real-time velocity along the horizontal and angled lanes.
		public float WhamTravel => Travel(0).Length;
		public Vector2 Travel(int row) => Skin.Direction(row) * Skin.ScrollLength * MovementUnit;
		public Vector2 PositionAt(int column, double time)
		{
			if (column == 12) return WhamReceiver + new Vector2(WhamTravel * (float)((time - CurrentTime) / VisibleTime), 0);
			int row = column / 6;
			return Receiver(row, column % 6) - Travel(row) * (float)((time - CurrentTime) / VisibleTime);
		}

		public float WhamLineWidth => Math.Max(1, Unit);
		public Vector2 WhamLineCentre => ShredWhamGeometry.JudgementCentre(Skin, Receiver(0, 5), Receiver(1, 5), Unit, WhamLineWidth);
		public Vector2 WhamReceiver => ShredWhamGeometry.ContactEdge(WhamLineCentre, WhamLineWidth);
		public float WhamGridHeight => ShredWhamGeometry.LineHeight(Skin) * Unit;
		public Vector2 WhamSize => ShredWhamGeometry.BarSize(Skin, Unit);
		public float WhamGridLeft => WhamReceiver.X - Skin.ColumnSpacing[3] * Unit;
		public bool IsWhamLane(Vector2 screen)
		{
			Vector2 local = ToLocalSpace(screen);
			return ContainsScreen(screen) && local.X >= WhamGridLeft && Math.Abs(local.Y - WhamReceiver.Y) <= WhamGridHeight / 2;
		}
        public double TimeAt(Vector2 screen, int column)
        {
            Vector2 local = ToLocalSpace(screen);
            if (column == 12) return CurrentTime + (local.X - WhamReceiver.X) / (WhamTravel) * VisibleTime;
            int row = column / 6;
            return CurrentTime - (local.Y - Receiver(row, 1).Y) / Travel(row).Y * VisibleTime;
        }
        public bool ContainsScreen(Vector2 screen)
        {
            Vector2 local = ToLocalSpace(screen);
            return local.X >= 0 && local.X <= DrawWidth && local.Y >= 0 && local.Y <= DrawHeight;
        }
        public bool TryLocate(Vector2 screen, out int column, out double time)
        {
            Vector2 local = ToLocalSpace(screen);
            column = -1;
            time = 0;
            if (!ContainsScreen(screen) || Unit <= 0) return false;
            // Give the entire sheared receptor the same placement area as a short note.
            for (int r = 0; r < 2; r++)
            {
                for (int f = 1; f <= 5; f++)
                {
                    Vector2 delta = local - Receiver(r, f);
                    Vector2 size = NoteSize(f);
                    Vector2 direction = Skin.Direction(r);
                    float dx = delta.X - delta.Y * direction.X / direction.Y;
                    if (Math.Abs(delta.Y) <= size.Y / 2 && Math.Abs(dx) <= size.X / 2)
                    { column = r * 6 + f; time = CurrentTime; return true; }
                }
            }
            if (IsWhamLane(screen))
            {
                column = 12;
                time = TimeAt(screen, column);
                return time >= CurrentTime - 0.001;
            }
            int row = 0;
            if (local.Y >= DrawHeight / 2) row = 1;
            time = TimeAt(screen, row * 6);
            if (time < CurrentTime - 0.001) return false;
            Vector2 offset = -Travel(row) * (float)((time - CurrentTime) / VisibleTime);
            float x = local.X - offset.X;
            float left = Receiver(row, 1).X - Skin.ColumnWidth[0] * Unit / 2;
            float right = Receiver(row, 5).X + Skin.ColumnWidth[4] * Unit / 2;
            if (x < left - 40 * Unit || x > right + 40 * Unit) return false;
            if (x < left || x > right) { column = row * 6; return true; }
            float nearest = float.MaxValue;
            for (int fret = 1; fret <= 5; fret++)
            {
                float distance = Math.Abs(x - Receiver(row, fret).X);
                if (distance < nearest) { nearest = distance; column = row * 6 + fret; }
            }
            return column >= 0;
        }
        public bool ScrollAtEdge(Vector2 screen, int column, double elapsedMs, double speed = 1)
        {
            Vector2 local = ToLocalSpace(screen);
            const float edge = 60;
            double strength;
            if (column == 12)
                strength = Math.Max(0, 1 - (DrawWidth - local.X) / edge) - Math.Max(0, 1 - local.X / edge);
            else
            {
                int row = column / 6;
                float downstream = row == 0 ? 1 : -1;
                float pastReceptor = (local.Y - Receiver(row, 1).Y) * downstream - NoteSize(1).Y / 2;
                float toOuterEdge = row == 0 ? local.Y : DrawHeight - local.Y;
                // No backward scroll anywhere inside a fret case. It begins only
                // after crossing its downstream edge toward the other row.
                strength = Math.Max(0, 1 - toOuterEdge / edge) - Math.Max(0, pastReceptor / edge);
            }
            strength = Math.Clamp(strength, -1, 1);
            double target = Math.Clamp(CurrentTime + Math.Sign(strength) * strength * strength * VisibleTime * Math.Min(elapsedMs, 50) * speed / 500, 0, editorClock.TrackLength);
            if (Math.Abs(target - CurrentTime) < 0.000001) return false;
            editorClock.Seek(target);
            return true;
        }

		public bool NoteContains(ShredHitObject note, Vector2 screen)
		{
			if (note.EndTime < CurrentTime - 0.5 || note.StartTime > LastVisibleTime || !ContainsScreen(screen)) return false;
			Vector2 local = ToLocalSpace(screen);
			double visibleStart = Math.Max(CurrentTime, note.StartTime);
			if (note.IsWham)
			{
				if (local.X + Skin.Shear(0) * (local.Y - WhamReceiver.Y) < WhamReceiver.X) return false;
				return ShredWhamGeometry.ContainsVisibleBar(local, PositionAt(12, visibleStart),
					PositionAt(12, note.EndTime), WhamSize.Y, Skin.Shear(0), Unit) && !tailHeadContains(note, screen);
			}
			Vector2 head = PositionAt(note.Column, visibleStart);
			Vector2 size = NoteSize(note.Fret);
			Vector2 direction = Skin.Direction(note.Row);
			if (Math.Abs(direction.Y) < 0.0001f) return false;
			Vector2 tail = head;
			if (note.IsLong)
				tail = ShredNoteVisual.SustainEndAtUpstreamEdge(PositionAt(note.Column, note.EndTime), direction, size.Y);
			float minY = head.Y - size.Y / 2;
			float maxY = head.Y + size.Y / 2;
			if (note.IsLong) { minY = Math.Min(minY, tail.Y); maxY = Math.Max(maxY, tail.Y); }
			float receptorY = Receiver(note.Row, note.Fret).Y;
			if (direction.Y > 0) maxY = Math.Min(maxY, receptorY + size.Y / 2);
			else minY = Math.Max(minY, receptorY - size.Y / 2);
			if (local.Y < minY || local.Y > maxY) return false;
			float centreX = head.X + (local.Y - head.Y) * direction.X / direction.Y;
			return Math.Abs(local.X - centreX) <= size.X / 2 && !tailHeadContains(note, screen);
		}

        // A later head owns its entire visible surface, even when the sustain
        // ending beneath it is already selected and therefore drawn on top.
        public bool HeadContains(ShredHitObject note, Vector2 screen)
        {
            if (HasPassed(note.StartTime) || !ContainsScreen(screen)) return false;
            Vector2 local = ToLocalSpace(screen);
            Vector2 head = PositionAt(note.IsWham ? 12 : note.Column, note.StartTime);
            if (note.IsWham)
                return Math.Abs(local.Y - head.Y) <= WhamSize.Y / 2
                    && Math.Abs(local.X - head.X + Skin.Shear(0) * (local.Y - head.Y)) <= WhamSize.X / 2;
            Vector2 direction = Skin.Direction(note.Row);
            Vector2 size = NoteSize(note.Fret);
            float dy = local.Y - head.Y;
            return Math.Abs(dy) <= size.Y / 2 && Math.Abs(local.X - head.X - dy * direction.X / direction.Y) <= size.X / 2;
        }

        private bool isHeadAtTail(ShredHitObject sustain, ShredHitObject candidate)
            => candidate != sustain && candidate.IsWham == sustain.IsWham && (sustain.IsWham || candidate.Column == sustain.Column)
                && Math.Abs(candidate.StartTime - sustain.EndTime) < 0.5;

        private bool tailHeadContains(ShredHitObject note, Vector2 screen)
        {
            if (!note.IsLong) return false;
            // Check the small endpoint area before searching the beatmap.
            Vector2 end = PositionAt(note.IsWham ? 12 : note.Column, note.EndTime);
            Vector2 local = ToLocalSpace(screen);
            if (!note.IsWham && Math.Abs(local.Y - end.Y) > NoteSize(note.Fret).Y / 2) return false;
            if (note.IsWham && Math.Abs(local.X - end.X + Skin.Shear(0) * (local.Y - end.Y)) > WhamSize.X / 2) return false;
            return beatmap.HitObjects.OfType<ShredHitObject>().Any(n => isHeadAtTail(note, n) && HeadContains(n, screen));
        }

		public bool EndHandleContains(ShredHitObject note, Vector2 screen)
		{
			if (!note.IsLong || note.EndTime < CurrentTime || note.EndTime > LastVisibleTime || !ContainsScreen(screen)) return false;
			Vector2 local = ToLocalSpace(screen);
            double time;
            double halfHeadTime;
			if (note.IsWham)
			{
                float dy = local.Y - WhamReceiver.Y;
                if (Math.Abs(dy) > WhamSize.Y / 2 + 4 * Unit) return false;
                time = CurrentTime + (local.X + Skin.Shear(0) * dy - WhamReceiver.X) / WhamTravel * VisibleTime;
                halfHeadTime = WhamSize.X / 2 / WhamTravel * VisibleTime;
			}
            else
            {
                Vector2 centre = PositionAt(note.Column, note.EndTime);
                Vector2 size = NoteSize(note.Fret);
                Vector2 direction = Skin.Direction(note.Row);
                if (Math.Abs(direction.Y) < 0.0001f) return false;
                float dy = local.Y - centre.Y;
                float dx = local.X - centre.X - dy * direction.X / direction.Y;
                if (Math.Abs(dx) > size.X / 2 + 4 * Unit) return false;
                time = TimeAt(screen, note.Column);
                halfHeadTime = size.Y / 2 / Math.Abs(Travel(note.Row).Y) * VisibleTime;
            }
            if (time > note.EndTime + halfHeadTime || time < note.StartTime || HeadContains(note, screen)) return false;
            bool occupied = beatmap.HitObjects.OfType<ShredHitObject>().Any(n => isHeadAtTail(note, n));
            double to = note.EndTime + (occupied ? -halfHeadTime : halfHeadTime);
            // Start at the common 1/4 tick before the note's visible near edge,
            // or before the bare LN endpoint. This keeps the handle available
            // when zooming out makes a head wider than one subdivision.
            double from = Math.Max(note.StartTime, ShredGridTicks.Previous(beatmap.ControlPointInfo, 4,
                occupied ? to : note.EndTime));
            if (time < from - 0.001) return false;
            return time <= to && !tailHeadContains(note, screen);
		}

		// A dragged LN may have its actual head outside the visible editing area.
		// Resolve its column on the original row without clipping its time.
		public int DragColumnAt(Vector2 screen, int row)
		{
			Vector2 local = ToLocalSpace(screen);
			double time = TimeAt(screen, row * 6);
			Vector2 offset = -Travel(row) * (float)((time - CurrentTime) / VisibleTime);
			float x = local.X - offset.X;
            float left = Receiver(row, 1).X - NoteSize(1).X / 2;
            float right = Receiver(row, 5).X + NoteSize(5).X / 2;
            if ((x < left && x >= left - 40 * Unit) || (x > right && x <= right + 40 * Unit)) return row * 6;
			int nearest = 1;
			float distance = float.MaxValue;
			for (int fret = 1; fret <= 5; fret++)
			{
				float candidate = Math.Abs(x - Receiver(row, fret).X);
				if (candidate < distance) { distance = candidate; nearest = fret; }
			}
			if (distance > NoteSize(nearest).X / 2 + 40 * Unit) return -1;
			return row * 6 + nearest;
		}

		public int ColumnAt(Vector2 screen) => TryLocate(screen, out int column, out _) ? column : -1;
		public double TimeAt(Vector2 screen) => TryLocate(screen, out _, out double time) ? time : CurrentTime;
		public bool InGrid(Vector2 screen) => TryLocate(screen, out _, out _);

		[BackgroundDependencyLoader]
		private void load()
		{
			Masking = true;
			AddInternal(new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(15, 12, 24, 245) });
			for (int row = 0; row < 2; row++)
			{
				activeRows[row].Colour = new Color4(160, 130, 230, 255);
				AddInternal(activeRows[row]);
			}
			AddInternal(grid);
			grid.Add(whamLine);
			for (int row = 0; row < 2; row++)
			{
				stringLines[row] = new Box { Colour = new Color4(180, 175, 210, 120), Height = 1 };
				AddInternal(stringLines[row]);
				openLabels[row] = new SpriteText { Text = "OPEN", Origin = Anchor.Centre, Colour = Color4.White, Font = FontUsage.Default.With(size: 14) };
				AddInternal(openLabels[row]);
				for (int fret = 1; fret <= 5; fret++)
				{
					Color4 colour = ShredPlayfield.FretColour(fret);
					lanes[row, fret - 1] = new Box { Colour = new Color4(160, 165, 180, 100) };
					AddInternal(lanes[row, fret - 1]);
					receptors[row, fret - 1] = new ShredGlyph(colour, shear: Skin.Shear(row)) { Origin = Anchor.Centre };
					AddInternal(receptors[row, fret - 1]);
				}
				cursors[row] = new Box { Height = 2, Colour = Color4.Yellow };
				grid.Add(cursors[row]);
			}
			AddInternal(HitObjectContainer);
		}
        // Allocate only the lines a view has needed, then reuse them while scrolling.
        // A fixed 3 x 512 drawable pool made every frame traverse mostly empty lines.
        private Box getGridLine(int row, int index)
        {
            if (gridLines[row, index] == null)
            {
                var line = new Box { Height = 1, Depth = -1 };
                if (row == 2) line.Origin = Anchor.Centre;
                grid.Add(gridLines[row, index] = line);
            }
            return gridLines[row, index];
        }

        private void hideUnusedGridLines(int row, int count)
        {
            for (int i = count; i < gridLineCounts[row]; i++) gridLines[row, i].Alpha = 0;
            gridLineCounts[row] = count;
        }

		protected override void Update()
		{
			base.Update();
			if (Unit <= 0) return;
            // Conservative time bound for both fret rows and whams, including the
            // head/halo margin. A sustain starting earlier may still cross this view.
            double longest = Math.Max(DrawHeight / Math.Max(1, Math.Abs(Travel(0).Y)),
                DrawHeight / Math.Max(1, Math.Abs(Travel(1).Y)));
            longest = Math.Max(longest, DrawWidth / Math.Max(1, WhamTravel));
            visibleLeadTime = VisibleTime * (longest + 1);
			for (int row = 0; row < 2; row++)
			{
				activeRows[row].Position = new Vector2(0, row * DrawHeight / 2);
				activeRows[row].Size = new Vector2(DrawWidth, DrawHeight / 2);
				activeRows[row].Alpha = 0;
				// No row preselection highlight; paste preserves the source rows.
			}
            if (!editorClock.IsSeeking)
            {
            int crossingDivisor = ShredGridTicks.DivisorAt(beatmap.ControlPointInfo, beatDivisor.Value, CurrentTime);
            var judgementColour = BindableBeatDivisor.GetColourFor(crossingDivisor, colours);
            whamLine.Colour = judgementColour;
            foreach (var cursor in cursors) cursor.Colour = judgementColour;
            }
			whamLine.Position = WhamLineCentre;
			whamLine.Size = new Vector2(WhamLineWidth, WhamGridHeight);
			whamLine.Shear = new Vector2(Skin.Shear(0), 0);
			for (int row = 0; row < 2; row++)
			{
				Vector2 travel = Travel(row);
				float left = Receiver(row, 1).X - Skin.ColumnWidth[0] / 2 * Unit;
				float right = Receiver(row, 5).X + Skin.ColumnWidth[4] / 2 * Unit;
				stringLines[row].Position = new Vector2(0, Receiver(row, 1).Y);
				stringLines[row].Width = DrawWidth;
				openLabels[row].Position = Receiver(row, 1) - new Vector2((Skin.ColumnWidth[0] / 2 + 20) * Unit, 0);
				for (int fret = 1; fret <= 5; fret++)
				{
					ShredGlyph receptor = receptors[row, fret - 1];
					receptor.Position = Receiver(row, fret);
					receptor.Size = NoteSize(fret);
					Box lane = lanes[row, fret - 1];
					float extent = DrawHeight / Math.Max(1, Math.Abs(travel.Y));
					lane.Position = receptor.Position - travel * extent;
					lane.Size = new Vector2(travel.Length * extent, Unit);
					lane.Rotation = (float)(Math.Atan2(travel.Y, travel.X) * 180 / Math.PI);
				}
				cursors[row].Position = new Vector2(left, Receiver(row, 1).Y);
				cursors[row].Width = right - left;
				int count = 0;
				double topY = row == 0 ? 0 : DrawHeight / 2;
				double bottomY = row == 0 ? DrawHeight / 2 : DrawHeight;
				double atTop = CurrentTime - (topY - Receiver(row, 1).Y) / travel.Y * VisibleTime;
				double atBottom = CurrentTime - (bottomY - Receiver(row, 1).Y) / travel.Y * VisibleTime;
				double rangeStart = Math.Max(CurrentTime, Math.Min(atTop, atBottom));
				double rangeEnd = Math.Max(atTop, atBottom);
				foreach (ShredGridTick tick in ShredGridTicks.Enumerate(beatmap.ControlPointInfo, beatDivisor.Value, rangeStart, rangeEnd))
				{
					if (count >= max_grid_lines) break;
					Vector2 offset = -travel * (float)((tick.Time - CurrentTime) / VisibleTime);
					Box line = getGridLine(row, count++);
					line.Position = new Vector2(left + offset.X, Receiver(row, 1).Y + offset.Y);
					line.Width = right - left;
					line.Colour = BindableBeatDivisor.GetColourFor(tick.Divisor, colours);
					line.Height = 1;
					if (tick.Divisor <= 2) line.Height = 2;
					line.Alpha = 1;
				}
				hideUnusedGridLines(row, count);
			}
			int whamCount = 0;
			double whamStart = CurrentTime;
			double whamEnd = CurrentTime + (DrawWidth - WhamReceiver.X) / (WhamTravel) * VisibleTime;
			foreach (ShredGridTick tick in ShredGridTicks.Enumerate(beatmap.ControlPointInfo, beatDivisor.Value, whamStart, whamEnd))
			{
				if (whamCount >= max_grid_lines) break;
				Box line = getGridLine(2, whamCount++);
				line.Position = PositionAt(12, tick.Time);
				line.Size = new Vector2(1, WhamGridHeight);
				if (tick.Divisor <= 2) line.Width = 2;
				line.Shear = new Vector2(Skin.Shear(0), 0);
				line.Colour = BindableBeatDivisor.GetColourFor(tick.Divisor, colours);
				line.Alpha = 1;
			}
			hideUnusedGridLines(2, whamCount);
		}
	}
}
