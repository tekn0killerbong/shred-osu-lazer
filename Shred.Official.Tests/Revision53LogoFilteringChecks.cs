using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Rulesets.Shred;
using osu.Game.Rulesets.Shred.UI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

internal static class Revision53LogoFilteringChecks
{
    public static void Run(string previewPath)
    {
        using var loader = new ShredLogoTextureLoaderStore();
        using var upload = loader.Get(ShredLogoTextureLoaderStore.ResourceName);
        using var asyncUpload = loader.GetAsync(ShredLogoTextureLoaderStore.ResourceName).GetAwaiter().GetResult();
        using var sourceLoader = new TextureLoaderStore(new DllResourceStore(typeof(ShredRuleset).Assembly));
        using var sourceUpload = sourceLoader.Get(ShredLogoTextureLoaderStore.ResourceName);
        Check(sourceUpload.Width == 1391 && sourceUpload.Height == 1391, "original PNG remains embedded at full resolution");
        Check(upload.Width / 8.0 <= 20 && upload.Width == upload.Height, "automatic mip levels reach toolbar resolution without stretching");
        Check(upload.Data.SequenceEqual(asyncUpload.Data), "synchronous and asynchronous logo loads agree");
        Check(loader.Get("Resources/Fonts/ShredMods_0.png") == null, "mod glyph textures are not filtered by the logo loader");

        using var source = Image.LoadPixelData(sourceUpload.Data, sourceUpload.Width, sourceUpload.Height);
        using var filtered = Image.LoadPixelData(upload.Data, upload.Width, upload.Height);
        double sourceCoverage = sourceUpload.Data.ToArray().Average(p => p.A / 255.0);
        double filteredCoverage = upload.Data.ToArray().Average(p => p.A / 255.0);
        Check(Math.Abs(sourceCoverage - filteredCoverage) < 0.005, "filter preserves logo coverage within half a percentage point");
        Check(upload.Data.ToArray().Any(p => p.A > 16 && p.A < 239), "filtered texture contains gradual alpha edges");

        using var comparison = new Image<Rgba32>(640, 384, new Rgba32(35, 32, 42));
        int row = 0;
        foreach (int size in new[] { 20, 24, 32 })
        {
            // Diagnostic approximation of the three-level trilinear GPU sampler, not a game screenshot.
            using var before = Sample(source, size);
            using var after = Sample(filtered, size);
            using var reference = source.Clone(c => c.Resize(size, size, KnownResamplers.Box));
            double beforeError = Error(before, reference);
            double afterError = Error(after, reference);
            Console.WriteLine($"FILTER {size}px: old alpha error={beforeError:F4}, filtered={afterError:F4}");
            int beforeEdges = PartialPixels(before), afterEdges = PartialPixels(after);
            Console.WriteLine($"FILTER {size}px: gradual edge pixels old={beforeEdges}, filtered={afterEdges}");
            Check(afterEdges > beforeEdges, $"{size}px reduction produces more gradual edge coverage");
            comparison.Mutate(c => c.DrawImage(before, new Point(8, row * 128 + 8), 1)
                .DrawImage(after, new Point(328, row * 128 + 8), 1));
            before.Mutate(c => c.Resize(size * 3, size * 3, KnownResamplers.NearestNeighbor));
            after.Mutate(c => c.Resize(size * 3, size * 3, KnownResamplers.NearestNeighbor));
            comparison.Mutate(c => c.DrawImage(before, new Point(64, row * 128 + 8), 1)
                .DrawImage(after, new Point(384, row * 128 + 8), 1));
            row++;
        }
        if (!string.IsNullOrEmpty(previewPath)) comparison.SaveAsPng(previewPath);
    }

    private static Image<Rgba32> Sample(Image<Rgba32> original, int size)
    {
        double lod = Math.Clamp(Math.Log2(original.Width / (double)size), 0, 3);
        int lower = (int)Math.Floor(lod);
        int upper = (int)Math.Ceiling(lod);
        using var a = Mip(original, lower);
        using var b = Mip(original, upper);
        var result = new Image<Rgba32>(size, size);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            double first = BilinearAlpha(a, x, y, size);
            double second = BilinearAlpha(b, x, y, size);
            result[x, y] = new Rgba32(255, 255, 255, (byte)Math.Clamp(Math.Round(first + (second - first) * (lod - lower)), 0, 255));
        }
        return result;
    }

    private static Image<Rgba32> Mip(Image<Rgba32> source, int level)
    {
        var image = source.Clone();
        for (int i = 0; i < level; i++)
            image.Mutate(c => c.Resize(image.Width / 2, image.Height / 2, KnownResamplers.Box));
        return image;
    }

    private static double BilinearAlpha(Image<Rgba32> image, int x, int y, int size)
    {
        double px = (x + 0.5) * image.Width / size - 0.5;
        double py = (y + 0.5) * image.Height / size - 0.5;
        int x0 = (int)Math.Floor(px), y0 = (int)Math.Floor(py);
        double fx = px - x0, fy = py - y0;
        double Pixel(int dx, int dy) => image[Math.Clamp(x0 + dx, 0, image.Width - 1), Math.Clamp(y0 + dy, 0, image.Height - 1)].A;
        return (Pixel(0, 0) * (1 - fx) + Pixel(1, 0) * fx) * (1 - fy)
            + (Pixel(0, 1) * (1 - fx) + Pixel(1, 1) * fx) * fy;
    }

    private static double Error(Image<Rgba32> a, Image<Rgba32> b)
    {
        double error = 0;
        for (int y = 0; y < a.Height; y++)
        for (int x = 0; x < a.Width; x++) error += Math.Abs(a[x, y].A - b[x, y].A) / 255.0;
        return error / (a.Width * a.Height);
    }

    private static int PartialPixels(Image<Rgba32> image)
    {
        int count = 0;
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
            if (image[x, y].A > 16 && image[x, y].A < 239) count++;
        return count;
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
}
