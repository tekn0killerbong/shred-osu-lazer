using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using osu.Framework.Bindables;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Shred.Edit
{
	// Scoped to one editor beatmap. No static preferences can leak into gameplay
	// or another editor, and restoring history never performs a fresh resnap.
	public sealed class ShredEditorSnapState
	{
		private static readonly ConditionalWeakTable<EditorBeatmap, ShredEditorSnapState> states = new();
		public static ShredEditorSnapState For(EditorBeatmap beatmap) => states.GetValue(beatmap, _ => new ShredEditorSnapState());
		public readonly BindableBool MoveWithTiming = new BindableBool();
		public readonly BindableBool SnapMovedObjects = new BindableBool(false);
		// Explicit Shred group operations are already transformed before Update.
		internal readonly HashSet<ShredHitObject> HandledGroups = new();
		internal readonly HashSet<ShredHitObject> PendingMovement = new();
		public Func<bool> PointerHeld;
		public Func<bool> CanEdit;
		public Action<IEnumerable<ShredHitObject>> ResolveConflicts;
		public bool Active => CanEdit?.Invoke() == true;
		internal void FinishMovement(EditorBeatmap beatmap)
		{
			var moved = PendingMovement.Where(beatmap.HitObjects.Contains).ToArray();
			PendingMovement.Clear();
			if (Active && moved.Length > 0) ResolveConflicts?.Invoke(moved);
		}
		private TimingSnapshot[] timing = Array.Empty<TimingSnapshot>();
		private sealed class TimingSnapshot
		{
			public TimingControlPoint Point;
			public double Time;
			public double BeatLength;
		}

		public Func<double, bool> ChangedSections(ControlPointInfo current)
		{
			TimingSnapshot[] before = timing;
			timing = current.TimingPoints.Select(p => new TimingSnapshot { Point = p, Time = p.Time, BeatLength = p.BeatLength }).ToArray();
			var changed = before.Where(p => current.TimingPoints.Contains(p.Point)
				&& (p.Time != p.Point.Time || p.BeatLength != p.Point.BeatLength)).ToHashSet();
			if (!Active || !MoveWithTiming.Value || changed.Count == 0) return _ => false;
			return time =>
			{
				TimingSnapshot section = before.LastOrDefault(p => p.Time <= time) ?? before.FirstOrDefault();
				return section != null && changed.Contains(section);
			};
		}
	}
}
