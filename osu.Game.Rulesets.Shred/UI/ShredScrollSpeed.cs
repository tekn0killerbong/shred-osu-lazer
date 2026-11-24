using System;
using System.Globalization;
using System.IO;
using osu.Framework.Bindables;

namespace osu.Game.Rulesets.Shred.UI
{
	public static class ShredScrollSpeed
	{
		private const int default_speed = 8;
		// Maximum editor rate (2x) combined with the maximum DT rate (2x).
		public const double MaximumPlaybackRate = 4;
		public const double DefaultApproachTime = 11485.0 / default_speed;
		private static readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "osu-shred", "scroll-speed.txt");
		public static readonly BindableInt Setting = new BindableInt(default_speed)
        {
            MinValue = 1, MaxValue = 40, Default = default_speed, Value = load()
        };
        static ShredScrollSpeed()
        {
            Setting.BindValueChanged(_ => save());
        }

		public static int Speed => Setting.Value;
		// Real milliseconds for the full regular-note travel, independent of playback rate.
		public static double ApproachTime => 11485.0 / Speed;

		public static double ValidPlaybackRate(double rate)
		{
			if (double.IsFinite(rate) && rate != 0) return Math.Abs(rate);
			return 1;
		}

		public static double MapDuration(double realDuration, double rate)
			=> realDuration * ValidPlaybackRate(rate);

		public static void Adjust(int change)
		{
			Setting.Value = Math.Clamp(Speed + change, 1, 40);
        }
        private static void save()
        {
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, Speed.ToString(CultureInfo.InvariantCulture));
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
		}

		private static int load()
		{
			try
			{
				if (int.TryParse(File.ReadAllText(path), NumberStyles.Integer, CultureInfo.InvariantCulture, out int saved))
					return Math.Clamp(saved, 1, 40);
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
			return default_speed;
		}
	}
}
