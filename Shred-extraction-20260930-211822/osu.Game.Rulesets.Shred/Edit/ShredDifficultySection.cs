using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Setup;
namespace osu.Game.Rulesets.Shred.Edit
{
	public partial class ShredDifficultySection : SetupSection
	{
		public override LocalisableString Title => "Shred";
		[Resolved] private IEditorChangeHandler changeHandler { get; set; }
		[BackgroundDependencyLoader]
		private void load()
		{
			Beatmap.Difficulty.CircleSize = 12;
			Add(new SpriteText { Text = "2 strings — OPEN + 5 frets each", Font = FontUsage.Default.With(size: 18) });
			FormSliderBar<float> difficulty = new FormSliderBar<float>
			{
				Caption = "Accuracy (OD)",
				Current = new BindableFloat(Beatmap.Difficulty.OverallDifficulty) { MinValue = 0, MaxValue = 10, Precision = 0.1f },
				TransferValueOnCommit = true,
				TabbableContentContainer = this
			};
			Add(difficulty);
            var drain = new FormSliderBar<float>
            {
                Caption = "HP Drain",
                Current = new BindableFloat(Beatmap.Difficulty.DrainRate) { MinValue = 0, MaxValue = 10, Precision = 0.1f },
                TransferValueOnCommit = true,
                TabbableContentContainer = this
            };
            Add(drain);
            drain.Current.BindValueChanged(value =>
            {
                changeHandler.BeginChange();
                Beatmap.Difficulty.DrainRate = value.NewValue;
                Beatmap.UpdateAllHitObjects();
                changeHandler.EndChange();
            });
			difficulty.Current.BindValueChanged(value =>
			{
				changeHandler.BeginChange();
				Beatmap.Difficulty.OverallDifficulty = value.NewValue;
				Beatmap.UpdateAllHitObjects();
				changeHandler.EndChange();
			});
		}
	}
}
