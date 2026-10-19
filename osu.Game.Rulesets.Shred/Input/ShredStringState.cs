using System;

namespace osu.Game.Rulesets.Shred.Input
{
	// Pure input state: no graphics, keyboard positions or timing judgements.
	public sealed class ShredStringState
	{
		private readonly bool[] frets = new bool[5];
		private readonly bool[] picks = new bool[4];

		// Zero represents an open string. Frets are numbered 1 to 5.
		public int SelectedFret
		{
			get
			{
				for (int index = 0; index < frets.Length; index++)
				{
					if (frets[index])
						return index + 1;
				}
				return 0;
			}
		}

		public bool IsFretHeld(int index) => frets[index];
		public int FretMask
		{
			get
			{
				int mask = 0;
				for (int i = 0; i < frets.Length; i++) if (frets[i]) mask |= 1 << i;
				return mask;
			}
		}
		public bool IsPickHeld(int index) => picks[index];
		public int PickMask
        {
            get
            {
                int mask = 0;
                for (int i = 0; i < picks.Length; i++) if (picks[i]) mask |= 1 << i;
                return mask;
            }
        }

        public void SetFret(int index, bool pressed)
		{
			frets[index] = pressed;
		}

		// Every pick has its own edge; keyboard repeat produces no extra pick.
		public bool SetPick(int index, bool pressed)
		{
			bool triggered = pressed && !picks[index];
			picks[index] = pressed;
			return triggered;
		}

		public void Reset()
		{
			Array.Clear(frets, 0, frets.Length);
			Array.Clear(picks, 0, picks.Length);
		}
	}
}
