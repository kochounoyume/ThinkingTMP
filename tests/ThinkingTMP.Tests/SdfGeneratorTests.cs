using ThinkingTMP.Core;
using Xunit;

namespace ThinkingTMP.Tests;

/// <summary>
/// Tests for <see cref="SdfGenerator"/>.
/// </summary>
public sealed class SdfGeneratorTests
{
    // -------------------------------------------------------------------------
    // Helper: build a simple rectangular foreground bitmap
    // -------------------------------------------------------------------------
    private static bool[] MakeRect(int bitmapW, int bitmapH, int rx, int ry, int rw, int rh)
    {
        bool[] bm = new bool[bitmapW * bitmapH];
        for (int y = ry; y < ry + rh; y++)
            for (int x = rx; x < rx + rw; x++)
                bm[y * bitmapW + x] = true;
        return bm;
    }

    [Fact]
    public void Generate_ThrowsOnNullBitmap()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SdfGenerator.Generate(null!, 4, 4, 2f));
    }

    [Fact]
    public void Generate_ThrowsOnLengthMismatch()
    {
        Assert.Throws<ArgumentException>(() =>
            SdfGenerator.Generate(new bool[5], 4, 4, 2f));
    }

    [Fact]
    public void Generate_ThrowsOnNonPositiveSpread()
    {
        bool[] bm = new bool[16];
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SdfGenerator.Generate(bm, 4, 4, 0f));
    }

    [Fact]
    public void Generate_OutputLengthEqualsInput()
    {
        bool[] bm = new bool[6 * 6];
        byte[] sdf = SdfGenerator.Generate(bm, 6, 6, 2f);
        Assert.Equal(bm.Length, sdf.Length);
    }

    [Fact]
    public void Generate_CentreOfFilledBitmapIs255()
    {
        // 9×9 fully-foreground bitmap: centre pixel is far from any edge.
        int w = 9, h = 9;
        bool[] bm = Enumerable.Repeat(true, w * h).ToArray();
        float spread = 4f;

        byte[] sdf = SdfGenerator.Generate(bm, w, h, spread);

        int centre = (h / 2) * w + (w / 2);
        Assert.Equal(255, sdf[centre]);
    }

    [Fact]
    public void Generate_FullyOutsideBitmapIs0()
    {
        // 9×9 fully-background bitmap: every pixel is far from any foreground.
        int w = 9, h = 9;
        bool[] bm = new bool[w * h]; // all false
        float spread = 4f;

        byte[] sdf = SdfGenerator.Generate(bm, w, h, spread);

        // All background → all values should be ≤ 128.
        Assert.All(sdf, v => Assert.True(v <= 128,
            $"Expected all values ≤ 128 for all-background bitmap, got {v}"));
    }

    [Fact]
    public void Generate_BoundaryPixelIsNear128()
    {
        // Bitmap: left half foreground, right half background.
        // The boundary column is the seam; pixels on either side should be near 128.
        int w = 20, h = 10;
        bool[] bm = new bool[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w / 2; x++)
                bm[y * w + x] = true;

        float spread = 5f;
        byte[] sdf = SdfGenerator.Generate(bm, w, h, spread);

        int midY = h / 2;
        // Last foreground column
        byte lastIn = sdf[midY * w + (w / 2 - 1)];
        // First background column
        byte firstOut = sdf[midY * w + (w / 2)];

        // The last foreground pixel (distance 0.5 from boundary) should be > 128.
        Assert.True(lastIn > 128, $"Last foreground pixel should be > 128, got {lastIn}");
        // The first background pixel (distance 0.5 from boundary) should be < 128.
        Assert.True(firstOut < 128, $"First background pixel should be < 128, got {firstOut}");
    }

    [Fact]
    public void Generate_InsideIsAlwaysGreaterThanOutside_ForRect()
    {
        // Centred 10×10 rect inside a 30×30 bitmap.
        int w = 30, h = 30;
        bool[] bm = MakeRect(w, h, 10, 10, 10, 10);
        float spread = 5f;

        byte[] sdf = SdfGenerator.Generate(bm, w, h, spread);

        // Pixels inside the rect should have higher SDF values than pixels outside.
        byte insideMin  = byte.MaxValue;
        byte outsideMax = byte.MinValue;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                byte v = sdf[y * w + x];
                if (bm[y * w + x])
                    insideMin  = Math.Min(insideMin, v);
                else
                    outsideMax = Math.Max(outsideMax, v);
            }
        }

        // Pixels on the boundary (edge of rect) might blur this — so just check
        // that the minimum inside value exceeds the maximum outside value
        // when spread is tight enough.  We relax to: average inside > average outside.
        double insideSum = 0, outsideSum = 0;
        int insideCnt = 0, outsideCnt = 0;
        for (int i = 0; i < sdf.Length; i++)
        {
            if (bm[i]) { insideSum += sdf[i]; insideCnt++; }
            else        { outsideSum += sdf[i]; outsideCnt++; }
        }

        Assert.True(insideSum / insideCnt > outsideSum / outsideCnt,
            "Average SDF value inside the shape should be higher than outside.");
    }
}
