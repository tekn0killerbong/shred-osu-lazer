using System;
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
		public double VisibleTime => ShredEditorOperations.VisibleTime(beatmap.TimelineZoom);

		public float MovementUnit => Math.Min(DrawWidth / 640, DrawHeight / 500);
		public float Unit => MovementUnit * ShredPlayfieldGeometry.VisualScale;
		public float ColumnWidth => Skin.ColumnWidth[0] * Unit;
		public double MinimumDuration(double time) => beatmap.ControlPointInfo.TimingPointAt(time).BeatLength / beatDivisor.Value;
		public double NextStep(double position) => ShredGridTicks.Next(beatmap.ControlPointInfo, beatDivisor.Value, position);
		public double CurrentTime => editorClock.CurrentTime;
		private float centreShift => Skin.OpenCentre;

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
		// Same visual distance per beat along the horizontal and angled lanes.
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
		public float WhamGridHeight => Math.Max(4, Skin.RowGap - 10) * Unit;
		public Vector2 WhamSize => new Vector2(8, Math.Min(10, Math.Max(4, Skin.RowGap - 12))) * Unit;
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
        public bool ScrollAtEdge(Vector2 screen, int column, double elapsedMs)
        {
            Vector2 local = ToLocalSpace(screen);
            const float edge = 60;
            double strength;
            if (column == 12)
                strength = Math.Max(0, 1 - (DrawWidth - local.X) / edge) - Math.Max(0, 1 - local.X / edge);
            else
            {
                float distanceToEdge = local.Y;
                float distanceToCentre = DrawHeight / 2 - local.Y;
                if (column / 6 == 1)
                {
                    distanceToEdge = DrawHeight - local.Y;
                    distanceToCentre = local.Y - DrawHeight / 2;
                }
                // The opposite grid never acts as this grid's backwards scrolling edge.
                strength = 0;
                if (distanceToCentre >= 0)
                    strength = Math.Max(0, 1 - distanceToEdge / edge) - Math.Max(0, 1 - distanceToCentre / edge);
            }
            strength = Math.Clamp(strength, -1, 1);
            double target = Math.Clamp(CurrentTime + Math.Sign(strength) * strength * strength * VisibleTime * Math.Min(elapsedMs, 50) / 500, 0, editorClock.TrackLength);
            if (Math.Abs(target - CurrentTime) < 0.000001) return false;
            editorClock.Seek(target);
            return true;
        }

		public bool NoteContains(ShredHitObject note, Vector2 screen)
		{
			if (!ContainsScreen(screen) || note.EndTime < CurrentTime - 0.5) return false;
			Vector2 local = ToLocalSpace(screen);
			double visibleStart = Math.Max(CurrentTime, note.StartTime);
			if (note.IsWham)
			{
				if (local.X < WhamReceiver.X) return false;
				return ShredWhamGeometry.ContainsVisibleBar(local, PositionAt(12, visibleStart),
					PositionAt(12, note.EndTime), WhamSize.Y, Skin.Shear(0), Unit);
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
			return Math.Abs(local.X - centreX) <= size.X / 2;
		}

		public bool EndHandleContains(ShredHitObject note, Vector2 screen)
		{
			if (!note.IsLong || !NoteContains(note, screen)) return false;
			Vector2 local = ToLocalSpace(screen);
			if (note.IsWham)
			{
				Vector2 end = PositionAt(12, note.EndTime);
				return Math.Abs(local.X - end.X) <= WhamSize.X / 2
					&& Math.Abs(local.Y - end.Y) <= WhamSize.Y / 2;
			}
			Vector2 centre = PositionAt(note.Column, note.EndTime);
			Vector2 size = NoteSize(note.Fret);
			Vector2 direction = Skin.Direction(note.Row);
			if (Math.Abs(direction.Y) < 0.0001f) return false;
			float dy = local.Y - centre.Y;
			float dx = local.X - centre.X - dy * direction.X / direction.Y;
			return Math.Abs(dy) <= size.Y / 2 && Math.Abs(dx) <= size.X / 2;
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
			AddInternal(whamLine);
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
				for (int i = 0; i < max_grid_lines; i++)
				{
					gridLines[row, i] = new Box { Height = 1 };
					grid.Add(gridLines[row, i]);
				}
				cursors[row] = new Box { Height = 2, Colour = Color4.Yellow };
				grid.Add(cursors[row]);
			}
			for (int i = 0; i < max_grid_lines; i++)
			{
				gridLines[2, i] = new Box { Origin = Anchor.Centre };
				grid.Add(gridLines[2, i]);
			}
			AddInternal(HitObjectContainer);
		}
		protected override void Update()
		{
			base.Update();
			if (Unit <= 0) return;
			for (int row = 0; row < 2; row++)
			{
				activeRows[row].Position = new Vector2(0, row * DrawHeight / 2);
				activeRows[row].Size = new Vector2(DrawWidth, DrawHeight / 2);
				activeRows[row].Alpha = 0;
				if (row == ActiveRow) activeRows[row].Alpha = 0.055f;
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
					Box line = gridLines[row, count++];
					line.Position = new Vector2(left + offset.X, Receiver(row, 1).Y + offset.Y);
					line.Width = right - left;
					line.Colour = BindableBeatDivisor.GetColourFor(tick.Divisor, colours);
					line.Height = 1;
					if (tick.Divisor <= 2) line.Height = 2;
					line.Alpha = 1;
				}
				while (count < max_grid_lines) gridLines[row, count++].Alpha = 0;
			}
			int whamCount = 0;
			double whamStart = CurrentTime;
			double whamEnd = CurrentTime + (DrawWidth - WhamReceiver.X) / (WhamTravel) * VisibleTime;
			foreach (ShredGridTick tick in ShredGridTicks.Enumerate(beatmap.ControlPointInfo, beatDivisor.Value, whamStart, whamEnd))
			{
				if (whamCount >= max_grid_lines) break;
				Box line = gridLines[2, whamCount++];
				line.Position = PositionAt(12, tick.Time);
				line.Size = new Vector2(1, WhamGridHeight);
				if (tick.Divisor <= 2) line.Width = 2;
				line.Shear = new Vector2(Skin.Shear(0), 0);
				line.Colour = BindableBeatDivisor.GetColourFor(tick.Divisor, colours);
				line.Alpha = 1;
			}
			while (whamCount < max_grid_lines) gridLines[2, whamCount++].Alpha = 0;
		}
	}
}
