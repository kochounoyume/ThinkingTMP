using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ThinkingTMP.Core;
using Xunit;

namespace ThinkingTMP.Tests;

/// <summary>
/// Tests for <see cref="AtlasPacker"/>.
/// </summary>
public sealed class AtlasPackerTests
{
    private static Dictionary<uint, (byte[], int, int)> MakeGlyphs(
        params (uint id, int w, int h)[] specs)
    {
        var dict = new Dictionary<uint, (byte[], int, int)>();
        foreach (var (id, w, h) in specs)
        {
            byte[] data = Enumerable.Repeat((byte)200, w * h).ToArray();
            dict[id] = (data, w, h);
        }
        return dict;
    }

    [Fact]
    public void Pack_EmptyInput_ReturnsEmptyPlacements()
    {
        var glyphs = new Dictionary<uint, (byte[], int, int)>();
        var (atlas, placements) = AtlasPacker.Pack(glyphs, 128, 128, 0);
        using (atlas)
        {
            Assert.Empty(placements);
            Assert.Equal(128, atlas.Width);
            Assert.Equal(128, atlas.Height);
        }
    }

    [Fact]
    public void Pack_SingleGlyph_IsPlacedAtTopLeft()
    {
        var glyphs = MakeGlyphs((1u, 20, 30));
        var (atlas, placements) = AtlasPacker.Pack(glyphs, 128, 128, 0);
        using (atlas)
        {
            Assert.Single(placements);
            var p = placements[0];
            Assert.Equal(1u, p.GlyphIndex);
            Assert.Equal(0,  p.X);
            Assert.Equal(0,  p.Y);
            Assert.Equal(20, p.Width);
            Assert.Equal(30, p.Height);
        }
    }

    [Fact]
    public void Pack_GlyphsTooLarge_AreSkipped()
    {
        // One glyph that exactly fits, one that doesn't.
        var glyphs = MakeGlyphs(
            (1u, 64,  64),
            (2u, 200, 200));  // larger than 128×128 atlas
        var (atlas, placements) = AtlasPacker.Pack(glyphs, 128, 128, 0);
        using (atlas)
        {
            Assert.Single(placements);
            Assert.DoesNotContain(placements, p => p.GlyphIndex == 2u);
        }
    }

    [Fact]
    public void Pack_MultipleGlyphs_NoOverlap()
    {
        // Pack four 30×30 glyphs into a 256×256 atlas.
        var glyphs = MakeGlyphs(
            (1u, 30, 30), (2u, 30, 30), (3u, 30, 30), (4u, 30, 30));
        var (atlas, placements) = AtlasPacker.Pack(glyphs, 256, 256, 0);
        using (atlas)
        {
            Assert.Equal(4, placements.Count);
            // Verify no two placements overlap.
            for (int i = 0; i < placements.Count; i++)
            {
                for (int j = i + 1; j < placements.Count; j++)
                {
                    var a = placements[i];
                    var b = placements[j];
                    bool overlap =
                        a.X < b.X + b.Width  && a.X + a.Width  > b.X &&
                        a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;
                    Assert.False(overlap,
                        $"Glyphs {a.GlyphIndex} and {b.GlyphIndex} overlap in atlas.");
                }
            }
        }
    }

    [Fact]
    public void Pack_AtlasPixelData_MatchesInput()
    {
        // Single 2×2 glyph with distinct value; verify it is blitted correctly.
        byte[] data = [10, 20, 30, 40];
        var glyphs = new Dictionary<uint, (byte[], int, int)>
        {
            [1u] = (data, 2, 2)
        };

        var (atlas, placements) = AtlasPacker.Pack(glyphs, 64, 64, 0);
        using (atlas)
        {
            var p = placements[0];
            // Sample pixels at placement position.
            byte[] actual = new byte[4];
            atlas.ProcessPixelRows(acc =>
            {
                actual[0] = acc.GetRowSpan(p.Y    )[p.X    ].PackedValue;
                actual[1] = acc.GetRowSpan(p.Y    )[p.X + 1].PackedValue;
                actual[2] = acc.GetRowSpan(p.Y + 1)[p.X    ].PackedValue;
                actual[3] = acc.GetRowSpan(p.Y + 1)[p.X + 1].PackedValue;
            });
            Assert.Equal(data, actual);
        }
    }

    [Fact]
    public void Pack_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AtlasPacker.Pack(null!, 128, 128, 0));
    }

    [Fact]
    public void Pack_ZeroAtlasWidth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AtlasPacker.Pack(new Dictionary<uint, (byte[], int, int)>(), 0, 128, 0));
    }
}
