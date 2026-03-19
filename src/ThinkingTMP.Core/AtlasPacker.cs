using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ThinkingTMP.Core;

/// <summary>
/// Packs individual SDF glyph bitmaps into a single atlas texture using a
/// shelf (Next-Fit Decreasing Height) bin-packing algorithm.
/// </summary>
public static class AtlasPacker
{
    /// <summary>
    /// Represents a placed glyph inside the atlas.
    /// </summary>
    public readonly struct Placement
    {
        public uint GlyphIndex { get; init; }
        /// <summary>Left edge of the glyph slot in atlas pixels.</summary>
        public int X { get; init; }
        /// <summary>Top edge of the glyph slot in atlas pixels (top-left origin).</summary>
        public int Y { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
    }

    /// <summary>
    /// Packs a collection of SDF glyph bitmaps into a grayscale atlas image.
    /// </summary>
    /// <param name="glyphs">
    ///   Map from glyph index to (sdfData, width, height).
    ///   <c>sdfData</c> is a row-major byte array (top-left origin) of the SDF values.
    /// </param>
    /// <param name="atlasWidth">Target atlas width in pixels.</param>
    /// <param name="atlasHeight">Target atlas height in pixels.</param>
    /// <param name="glyphPadding">Padding already included in each glyph's width/height.</param>
    /// <returns>
    ///   The populated atlas image (caller must dispose) and the placement list.
    ///   Glyphs that do not fit are silently skipped.
    /// </returns>
    public static (Image<L8> Atlas, List<Placement> Placements) Pack(
        IReadOnlyDictionary<uint, (byte[] SdfData, int Width, int Height)> glyphs,
        int atlasWidth,
        int atlasHeight,
        int glyphPadding)
    {
        ArgumentNullException.ThrowIfNull(glyphs);
        if (atlasWidth <= 0) throw new ArgumentOutOfRangeException(nameof(atlasWidth));
        if (atlasHeight <= 0) throw new ArgumentOutOfRangeException(nameof(atlasHeight));

        // Sort descending by height to improve packing density.
        var sorted = glyphs
            .OrderByDescending(kv => kv.Value.Height)
            .ThenByDescending(kv => kv.Value.Width)
            .ToList();

        var atlas = new Image<L8>(atlasWidth, atlasHeight);
        var placements = new List<Placement>(sorted.Count);

        // One inter-slot gap (1 px) between slots to avoid atlas bleeding.
        const int gap = 1;

        int cursorX = 0;
        int cursorY = 0;
        int shelfHeight = 0;

        foreach (var (glyphIndex, (sdfData, w, h)) in sorted)
        {
            if (w <= 0 || h <= 0) continue; // whitespace / empty glyph

            int slotW = w + gap;
            int slotH = h + gap;

            // Advance to next shelf if this glyph doesn't fit on the current one.
            if (cursorX + slotW > atlasWidth)
            {
                cursorX = 0;
                cursorY += shelfHeight;
                shelfHeight = 0;
            }

            if (cursorY + slotH > atlasHeight)
                continue; // atlas full — skip glyph

            // Blit SDF data into atlas.
            BlitGlyph(atlas, sdfData, cursorX, cursorY, w, h);

            placements.Add(new Placement
            {
                GlyphIndex = glyphIndex,
                X = cursorX,
                Y = cursorY,
                Width = w,
                Height = h,
            });

            cursorX += slotW;
            if (slotH > shelfHeight) shelfHeight = slotH;
        }

        return (atlas, placements);
    }

    private static void BlitGlyph(Image<L8> atlas, byte[] sdfData, int destX, int destY, int w, int h)
    {
        atlas.ProcessPixelRows(accessor =>
        {
            for (int row = 0; row < h; row++)
            {
                int atlasRow = destY + row;
                if (atlasRow >= accessor.Height) break;

                Span<L8> pixelRow = accessor.GetRowSpan(atlasRow);
                int srcOffset = row * w;
                for (int col = 0; col < w; col++)
                {
                    int atlasCol = destX + col;
                    if (atlasCol >= accessor.Width) break;
                    pixelRow[atlasCol] = new L8(sdfData[srcOffset + col]);
                }
            }
        });
    }
}
