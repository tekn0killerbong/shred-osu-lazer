using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.UI;
using osu.Game.Screens.Menu;
using osuTK;

internal partial class Revision51LogoChecks : OsuGame
{
    private readonly string folder;
    private readonly DateTime started = DateTime.UtcNow;
    private Exception failure;
    private bool passed;
    private bool toolbarChecked;
    private ConstrainedIconContainer sample;
    private int sampleIndex;
    private readonly Vector2[] sizes = { new(20), new(40), new(18, 22), new(48, 24), new(24, 48) };

    private Revision51LogoChecks(string folder) => this.folder = folder;

    protected override Storage CreateStorage(GameHost host, Storage defaultStorage) => new NativeStorage(folder);

    protected override void LoadComplete()
    {
        base.LoadComplete();
        Scheduler.AddDelayed(checkLayout, 250, true);
    }

    private static void checkIcon(ShredRulesetIcon icon, string label)
    {
        var quad = icon.ScreenSpaceDrawQuad;
        float width = Vector2.Distance(quad.TopLeft, quad.TopRight);
        float height = Vector2.Distance(quad.TopLeft, quad.BottomLeft);
        float expected = icon.Texture.DisplayWidth / icon.Texture.DisplayHeight;
        Console.WriteLine($"LOGO {label}: texture={icon.Texture.DisplaySize}, displayed={width:F4}x{height:F4}, ratio={width / height:F6}");
        if (width <= 0 || height <= 0 || Math.Abs(width / height - expected) > 0.001)
            throw new Exception($"Logo distorted in {label}: expected ratio {expected}, got {width / height}");
        if (icon.Texture.Width != ShredLogoTextureLoaderStore.TextureSize || icon.Texture.Height != ShredLogoTextureLoaderStore.TextureSize)
            throw new Exception("Logo texture did not use the pre-filtered square PNG");
        Console.WriteLine("PASS logo proportions and complete texture: " + label);
    }

    private void checkLayout()
    {
        try
        {
            if ((DateTime.UtcNow - started).TotalSeconds > 60) throw new Exception("Logo UI test timed out");
            if (ScreenStack?.CurrentScreen is not MainMenu) return;
            if (!toolbarChecked)
            {
                var icon = this.ChildrenOfType<ToolbarRulesetTabButton>()
                    .SelectMany(b => b.ChildrenOfType<ShredRulesetIcon>()).FirstOrDefault(i => i.IsLoaded);
                if (icon == null || icon.Texture == null || icon.DrawWidth == 0) return;
                checkIcon(icon, "actual official toolbar");
                toolbarChecked = true;
            }
            if (sample == null)
            {
                sample = new ConstrainedIconContainer
                {
                    Size = sizes[0],
                    Position = new Vector2(100),
                    Scale = new Vector2(1.75f),
                    Icon = new ShredRuleset().CreateIcon(),
                };
                Add(sample);
                return;
            }
            var sampleIcon = (ShredRulesetIcon)sample.Icon;
            if (!sampleIcon.IsLoaded || sampleIcon.Texture == null) return;
            checkIcon(sampleIcon, $"native constrained container {sizes[sampleIndex]} at 175% scale");
            sampleIndex++;
            if (sampleIndex < sizes.Length)
            {
                sample.Size = sizes[sampleIndex];
                return;
            }
            passed = true;
            Exit();
        }
        catch (Exception e)
        {
            failure = e;
            Console.WriteLine("LOGO FAILED: " + e.Message);
            Exit();
        }
    }

    public static void Run()
    {
        string folder = Path.Combine(Path.GetTempPath(), "shred-logo-ui-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(folder, "rulesets"));
        File.Copy(typeof(ShredRuleset).Assembly.Location, Path.Combine(folder, "rulesets", "osu.Game.Rulesets.Shred.dll"));
        using var host = new HeadlessGameHost("shred-logo-probe");
        using var game = new Revision51LogoChecks(folder);
        host.Run(game);
        if (!game.passed) throw new Exception("Logo UI test failed; logs: " + folder, game.failure);
    }
}
