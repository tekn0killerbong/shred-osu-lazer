using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Edit;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Shred.Beatmaps
{
	public sealed class ShredEditorBeatmapProcessor : BeatmapProcessor
	{
		private sealed class Position
		{
			public double Start;
			public double Duration;
			public ShredRhythmStamp Rhythm;
		}
		private readonly ConditionalWeakTable<ShredHitObject, Position> processed = new();
		private bool hasProcessed;
		private ShredBeatTimeline previousTiming;
		public ShredEditorBeatmapProcessor(IBeatmap beatmap) : base(beatmap) { }
		public override void PreProcess()
		{
			var legacy = new List<ShredHitObject>();
			var snapped = new List<ShredHitObject>();
			ShredEditorSnapState snapState = null;
			if (Beatmap is EditorBeatmap editorBeatmap) snapState = ShredEditorSnapState.For(editorBeatmap);
			var changedTiming = snapState?.ChangedSections(Beatmap.ControlPointInfo);
			var musicalTiming = new ShredBeatTimeline(Beatmap.ControlPointInfo);
			bool timingChanged = !musicalTiming.SameTiming(previousTiming);
			if (hasProcessed && snapState?.Active == true && snapState.SnapMovedObjects.Value)
			{
				// Native Paste deserialises the source's beat coordinates, then adds
				// a millisecond offset. Transform the complete batch before collision
				// trimming, using the one anchor already selected by native Paste.
				var pasted = Beatmap.HitObjects.Cast<ShredHitObject>()
					.Where(n => !processed.TryGetValue(n, out _) && !snapState.HandledGroups.Contains(n)
						&& n.EditorRhythm?.Valid == true).ToArray();
				if (pasted.Length > 0 && ShredGroupTiming.Apply(pasted.Select(n => (n, n.EditorRhythm)),
					pasted.Min(n => n.StartTime), musicalTiming)) snapped.AddRange(pasted);

				// Native timeline drags and nudges translate the current selection
				// uniformly. Convert that single displacement to musical intervals.
				// Tail resizing and explicit Shred group operations are excluded.
				var moved = Beatmap.HitObjects.Cast<ShredHitObject>()
					.Where(n => !snapState.HandledGroups.Contains(n) && processed.TryGetValue(n, out var p)
						&& p.Rhythm?.Valid == true && p.Start != n.StartTime && p.Duration == n.Duration
						&& changedTiming?.Invoke(p.Start) != true).ToArray();
				if (!timingChanged && moved.Length > 0)
				{
					double delta = moved[0].StartTime - processed.GetValue(moved[0], _ => new Position()).Start;
					if (moved.All(n => Math.Abs(n.StartTime - processed.GetValue(n, _ => new Position()).Start - delta) < 0.00001)
						&& ShredGroupTiming.Apply(moved.Select(n => (n, processed.GetValue(n, _ => new Position()).Rhythm)),
							moved.Min(n => n.StartTime), musicalTiming))
					{
						// A note crossed while dragging is not the final placement.
						// Defer cuts until mouse-up, within the same undo transaction.
						if (snapState.PointerHeld?.Invoke() == true) snapState.PendingMovement.UnionWith(moved);
						else snapped.AddRange(moved);
					}
				}
			}
			foreach (ShredHitObject note in Beatmap.HitObjects.ToArray())
			{
				bool knownGrid = ShredObjectSnap.HasGrid(note);
				if (!knownGrid) legacy.Add(note);
				if (!processed.TryGetValue(note, out var position))
				{
					position = new Position { Start = double.NaN };
					processed.Add(note, position);
				}
				bool timingMoved = changedTiming?.Invoke(position.Start) == true;
				// Only an explicit timing edit uses the legacy per-object subdivision.
				// Moves and pastes above use one shared musical anchor instead.
				if (hasProcessed && snapState?.Active == true && timingMoved)
				{
					if (ShredObjectSnap.Apply(note, Beatmap.ControlPointInfo)) snapped.Add(note);
				}
				if (knownGrid && (position.Start != note.StartTime || position.Duration != note.Duration))
					ShredGridTiming.PreserveTiming(note);
			}
			// Historical sub-ms repairs apply only to uninitialised legacy notes.
			// Reapplying them to valid translated notes subtly distorts copied groups.
			ShredGridTiming.Repair(legacy, Beatmap.ControlPointInfo);
			if (snapped.Count > 0) snapState?.ResolveConflicts?.Invoke(snapped);
			foreach (ShredHitObject note in Beatmap.HitObjects)
			{
				ShredObjectSnap.Initialise(note, Beatmap.ControlPointInfo);
				var position = processed.GetValue(note, _ => new Position());
				if (position.Rhythm == null || timingChanged || position.Start != note.StartTime || position.Duration != note.Duration)
					position.Rhythm = musicalTiming.Capture(note);
				note.EditorRhythm = position.Rhythm;
				position.Start = note.StartTime;
				position.Duration = note.Duration;
#if SHRED_OFFICIAL
				Compatibility.ShredEditorCodec.Synchronise(note);
#endif
			}
			hasProcessed = true;
			previousTiming = musicalTiming;
			snapState?.HandledGroups.Clear();
			base.PreProcess();
		}
	}
}
