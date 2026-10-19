using System.Collections.Generic;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Shred.Edit
{
	public sealed class ShredSelectionSweep
	{
		private readonly HashSet<HitObject> visited = new HashSet<HitObject>();
		private bool scrolled;
		public void Reset()
		{
			visited.Clear();
			scrolled = false;
		}
		public void Record(IEnumerable<HitObject> selected) => visited.UnionWith(selected);
		public void PreserveAfterScroll() => scrolled = true;
		public void AddPreservedTo(HashSet<HitObject> selection)
		{
			if (scrolled) selection.UnionWith(visited);
		}
	}
}
