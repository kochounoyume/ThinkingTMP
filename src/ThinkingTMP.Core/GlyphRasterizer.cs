using SixLabors.Fonts;
using SixLabors.Fonts.Unicode;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ThinkingTMP.Core.Models;

namespace ThinkingTMP.Core;

/// <summary>
/// Loads a font file, extracts face metrics and rasterizes individual glyphs
/// into signed-distance-field (SDF) bitmaps.
///
/// Workflow per glyph:
///   1. Render at <c>oversample × pointSize</c> into a temporary grayscale image.
///   2. Scan rendered pixels to locate the tight bounding box.
///   3. Crop + expand by <c>oversample × atlasPadding</c> on each side → the "super-sampled slot".
///   4. Generate SDF with spread = <c>oversample × atlasPadding</c>.
///   5. Downsample 1:oversample → the final atlas glyph slot.
/// </summary>
public sealed class GlyphRasterizer
{
    private readonly Font  _font;       // at pointSize for metric recording
    private readonly Font  _fontHigh;   // at pointSize × oversample for rendering
    private readonly int   _pointSize;
    private readonly int   _atlasPadding;
    private readonly int   _oversample;

    private readonly float _scale;     // pointSize / unitsPerEm
    private readonly float _scaleHigh; // (pointSize * oversample) / unitsPerEm

    public GlyphRasterizer(string fontPath, int pointSize, int atlasPadding, int oversample = 4)
    {
        if (string.IsNullOrWhiteSpace(fontPath))
            throw new ArgumentException("Font path must not be empty.", nameof(fontPath));
        if (!File.Exists(fontPath))
            throw new FileNotFoundException("Font file not found.", fontPath);
        if (pointSize <= 0)    throw new ArgumentOutOfRangeException(nameof(pointSize));
        if (atlasPadding < 0)  throw new ArgumentOutOfRangeException(nameof(atlasPadding));
        if (oversample < 1)    throw new ArgumentOutOfRangeException(nameof(oversample));

        var collection = new FontCollection();
        var family     = collection.Add(fontPath);

        _font     = family.CreateFont(pointSize);
        _fontHigh = family.CreateFont(pointSize * oversample);

        _pointSize    = pointSize;
        _atlasPadding = atlasPadding;
        _oversample   = oversample;

        var fm = _font.FontMetrics;
        _scale     = (float)pointSize / fm.UnitsPerEm;
        _scaleHigh = (float)(pointSize * oversample) / fm.UnitsPerEm;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Extracts font face information (in pixels at the given point size).
    /// </summary>
    public FaceInfo GetFaceInfo()
    {
        var fm = _font.FontMetrics;

        float ascender  =  fm.HorizontalMetrics.Ascender  * _scale;
        float descender =  fm.HorizontalMetrics.Descender * _scale;  // negative
        float lineGap   =  fm.HorizontalMetrics.LineGap   * _scale;

        // Superscript / subscript — many fonts omit OS/2 values; use heuristics.
        float xHeight  = ascender * 0.55f;
        float capLine  = ascender * 0.73f;

        // Underline and strikeout from the OS/2/post tables (design units).
        float ulOffset    = fm.UnderlinePosition  * _scale;
        float ulThick     = Math.Max(1f, fm.UnderlineThickness * _scale);
        float soOffset    = fm.StrikeoutPosition  * _scale;
        float soThick     = Math.Max(1f, fm.StrikeoutSize      * _scale);

        return new FaceInfo
        {
            FamilyName  = fm.Description.FontFamilyInvariantCulture  ?? string.Empty,
            StyleName   = fm.Description.FontSubFamilyNameInvariantCulture ?? string.Empty,
            PointSize   = _pointSize,
            Scale       = 1f,
            LineHeight  = ascender - descender + lineGap,
            AscentLine  = ascender,
            CapLine     = capLine,
            MeanLine    = xHeight,
            Baseline    = 0f,
            DescentLine = descender,
            SuperscriptOffset  =  ascender * 0.5f,
            SubscriptOffset    =  descender * 0.5f,
            SuperscriptSize    =  0.5f,
            SubscriptSize      =  0.5f,
            UnderlineOffset    =  ulOffset,
            UnderlineThickness =  ulThick,
            StrikethroughOffset    = soOffset,
            StrikethroughThickness = soThick,
            TabWidth = _pointSize * 40f / 90f,
        };
    }

    /// <summary>
    /// Rasterizes a codepoint into an SDF bitmap and returns its metrics.
    /// Returns <c>null</c> if the font does not contain a glyph for the codepoint.
    /// </summary>
    public RasterizedGlyph? RasterizeGlyph(uint codepoint)
    {
        var cp = new CodePoint((int)codepoint);

        // --- Low-res (target size) metrics ---
        if (!_font.TryGetGlyphs(cp, ColorFontSupport.None, out var glyphs) || glyphs.Count == 0)
            return null;

        var gm = glyphs[0].GlyphMetrics;

        float advancePx   = gm.AdvanceWidth    * _scale;
        float bearingXPx  = gm.LeftSideBearing * _scale;
        float glyphWidthPx  = gm.Width  * _scale;
        float glyphHeightPx = gm.Height * _scale;

        // BearingY: derive from TextMeasurer (ascender_px - charBound.Top).
        float ascenderPx = _font.FontMetrics.HorizontalMetrics.Ascender * _scale;
        float bearingYPx = glyphHeightPx; // fallback: full height above baseline
        {
            var opts = new TextOptions(_font);
            string text = char.ConvertFromUtf32((int)codepoint);
            if (TextMeasurer.TryMeasureCharacterBounds(text.AsSpan(), opts, out var bounds)
                && bounds.Length > 0)
            {
                bearingYPx = ascenderPx - bounds[0].Bounds.Y;
            }
        }

        // --- High-res rendering ---
        int spread = _oversample * _atlasPadding;

        var hmHigh      = _fontHigh.FontMetrics.HorizontalMetrics;
        float ascHigh   = hmHigh.Ascender * _scaleHigh;
        float descHigh  = hmHigh.Descender * _scaleHigh;       // negative
        float lineHHigh = ascHigh - descHigh;

        // Render into a buffer large enough for the full line-height + SDF spread.
        int renderW = Math.Max(1, (int)Math.Ceiling(advancePx * _oversample) + spread * 2 + 8);
        int renderH = Math.Max(1, (int)Math.Ceiling(lineHHigh) + spread * 2 + 8);

        // Origin: text origin at top-left, baseline is placed spread px below the top.
        // In ImageSharp DrawText, Y=0 is the top of the em-square.
        float originX = spread * 1.0f;
        float originY = spread * 1.0f;  // text top at Y=spread

        using var renderImg = new Image<L8>(renderW, renderH);
        renderImg.Mutate(ctx =>
        {
            ctx.Fill(Color.Black);
            var opts = new RichTextOptions(_fontHigh)
            {
                Origin           = new PointF(originX, originY),
                ColorFontSupport = ColorFontSupport.None,
            };
            ctx.DrawText(opts, char.ConvertFromUtf32((int)codepoint), Color.White);
        });

        // Find tight bounding box of rendered foreground pixels.
        var (tightMinX, tightMinY, tightMaxX, tightMaxY) = FindBounds(renderImg);
        bool isEmpty = tightMinX > tightMaxX || tightMinY > tightMaxY;

        byte[] sdfData;
        int slotW, slotH;

        if (isEmpty)
        {
            // Whitespace / invisible glyph — no SDF bitmap needed.
            sdfData = [];
            slotW   = 0;
            slotH   = 0;
        }
        else
        {
            // Expand tight bounds outward by the SDF spread.
            int cropX = Math.Max(0, tightMinX - spread);
            int cropY = Math.Max(0, tightMinY - spread);
            int cropX2 = Math.Min(renderW - 1, tightMaxX + spread);
            int cropY2 = Math.Min(renderH - 1, tightMaxY + spread);
            int cropW  = cropX2 - cropX + 1;
            int cropH  = cropY2 - cropY + 1;

            bool[] binaryBitmap = ExtractBinary(renderImg, cropX, cropY, cropW, cropH);
            byte[] sdfHigh      = SdfGenerator.Generate(binaryBitmap, cropW, cropH, spread);

            int dsW = (cropW + _oversample - 1) / _oversample;
            int dsH = (cropH + _oversample - 1) / _oversample;
            sdfData = Downsample(sdfHigh, cropW, cropH, dsW, dsH, _oversample);
            slotW   = dsW;
            slotH   = dsH;
        }

        var glyphData = new GlyphData
        {
            Index              = gm.GlyphId,
            MetricsWidth       = glyphWidthPx,
            MetricsHeight      = glyphHeightPx,
            HorizontalBearingX = bearingXPx,
            HorizontalBearingY = bearingYPx,
            HorizontalAdvance  = advancePx,
            AtlasWidth         = slotW,
            AtlasHeight        = slotH,
            Scale              = 1f,
            AtlasIndex         = 0,
        };

        return new RasterizedGlyph(glyphData, sdfData);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (int minX, int minY, int maxX, int maxY) FindBounds(Image<L8> img)
    {
        int minX = img.Width,  minY = img.Height;
        int maxX = -1,         maxY = -1;

        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<L8> row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x].PackedValue > 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
        });

        return (minX, minY, maxX, maxY);
    }

    private static bool[] ExtractBinary(Image<L8> img, int startX, int startY, int w, int h)
    {
        bool[] binary = new bool[w * h];
        img.ProcessPixelRows(accessor =>
        {
            for (int row = 0; row < h; row++)
            {
                int imgRow = startY + row;
                if (imgRow >= accessor.Height) break;
                Span<L8> pixels = accessor.GetRowSpan(imgRow);
                for (int col = 0; col < w; col++)
                {
                    int imgCol = startX + col;
                    if (imgCol >= pixels.Length) break;
                    binary[row * w + col] = pixels[imgCol].PackedValue > 127;
                }
            }
        });
        return binary;
    }

    /// <summary>Simple box-filter downsampling from (srcW × srcH) to (dstW × dstH).</summary>
    private static byte[] Downsample(byte[] src, int srcW, int srcH, int dstW, int dstH, int factor)
    {
        byte[] dst = new byte[dstW * dstH];
        for (int dy = 0; dy < dstH; dy++)
        {
            for (int dx = 0; dx < dstW; dx++)
            {
                long sum = 0;
                int  cnt = 0;
                int  sy0 = dy * factor;
                int  sx0 = dx * factor;
                for (int fy = 0; fy < factor && sy0 + fy < srcH; fy++)
                {
                    for (int fx = 0; fx < factor && sx0 + fx < srcW; fx++)
                    {
                        sum += src[(sy0 + fy) * srcW + (sx0 + fx)];
                        cnt++;
                    }
                }
                dst[dy * dstW + dx] = cnt > 0 ? (byte)(sum / cnt) : (byte)0;
            }
        }
        return dst;
    }
}

/// <summary>
/// Holds the rasterized SDF data and metadata for a single glyph.
/// </summary>
public sealed class RasterizedGlyph
{
    public GlyphData GlyphData { get; }
    /// <summary>
    /// SDF byte array (row-major, top-left origin).  Empty for invisible / whitespace glyphs.
    /// </summary>
    public byte[] SdfData { get; }

    public RasterizedGlyph(GlyphData glyphData, byte[] sdfData)
    {
        GlyphData = glyphData ?? throw new ArgumentNullException(nameof(glyphData));
        SdfData   = sdfData   ?? throw new ArgumentNullException(nameof(sdfData));
    }
}
