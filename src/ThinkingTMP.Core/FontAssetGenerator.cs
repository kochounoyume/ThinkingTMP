using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ThinkingTMP.Core.Models;

namespace ThinkingTMP.Core;

/// <summary>
/// Orchestrates font loading, glyph rasterisation, SDF generation, atlas packing,
/// and Unity asset file output.
/// </summary>
public sealed class FontAssetGenerator
{
    /// <summary>
    /// Generation settings.
    /// </summary>
    public sealed class Options
    {
        /// <summary>Point size at which to sample glyphs (e.g. 90).</summary>
        public int PointSize { get; init; } = 90;

        /// <summary>SDF padding on each side of each glyph in atlas pixels (e.g. 9).</summary>
        public int AtlasPadding { get; init; } = 9;

        /// <summary>Atlas texture width in pixels (must be power-of-two for best compatibility).</summary>
        public int AtlasWidth { get; init; } = 512;

        /// <summary>Atlas texture height in pixels (must be power-of-two for best compatibility).</summary>
        public int AtlasHeight { get; init; } = 512;

        /// <summary>
        /// Oversampling factor for rendering before SDF generation.
        /// Higher values give sharper SDFs but use more memory. Typical: 4.
        /// </summary>
        public int Oversample { get; init; } = 4;

        /// <summary>
        /// GlyphRenderMode written to the .asset (12287 = SDFAA, 8194 = SDF8).
        /// </summary>
        public int AtlasRenderMode { get; init; } = 12287; // SDFAA
    }

    /// <summary>
    /// Generates a <see cref="FontAssetData"/> object and the corresponding SDF atlas image.
    /// </summary>
    /// <param name="fontPath">Path to the .ttf / .otf file.</param>
    /// <param name="codepoints">Unicode code points to include in the atlas.</param>
    /// <param name="options">Generation options.</param>
    /// <returns>
    ///   <c>FontAssetData</c> (populated with metrics, tables and placeholder GUIDs) and
    ///   the atlas <see cref="Image{L8}"/> (caller must dispose).
    /// </returns>
    public static (FontAssetData Data, Image<L8> Atlas) Generate(
        string fontPath,
        IEnumerable<uint> codepoints,
        Options? options = null)
    {
        ArgumentNullException.ThrowIfNull(fontPath);
        ArgumentNullException.ThrowIfNull(codepoints);
        options ??= new Options();

        var rasterizer = new GlyphRasterizer(
            fontPath,
            options.PointSize,
            options.AtlasPadding,
            options.Oversample);

        FaceInfo faceInfo = rasterizer.GetFaceInfo();

        // Rasterize all requested codepoints.
        // Map: glyphId → (sdfData, width, height)
        var sdfMap      = new Dictionary<uint, (byte[], int, int)>();
        var glyphTable  = new List<GlyphData>();
        var charTable   = new List<CharacterData>();
        // Map glyphId → index in glyphTable (for deduplication)
        var glyphIndex  = new Dictionary<uint, int>();

        foreach (uint cp in codepoints.Distinct().Order())
        {
            RasterizedGlyph? rg = rasterizer.RasterizeGlyph(cp);
            if (rg is null) continue; // font doesn't have this codepoint

            uint glyphId = rg.GlyphData.Index;

            // Deduplicate: multiple codepoints can map to the same glyph (e.g. ligatures).
            if (!glyphIndex.TryGetValue(glyphId, out int tableIdx))
            {
                tableIdx = glyphTable.Count;
                glyphTable.Add(rg.GlyphData);
                glyphIndex[glyphId] = tableIdx;

                if (rg.SdfData.Length > 0)
                    sdfMap[glyphId] = (rg.SdfData, rg.GlyphData.AtlasWidth, rg.GlyphData.AtlasHeight);
            }

            charTable.Add(new CharacterData
            {
                Unicode    = cp,
                GlyphIndex = glyphId,
                Scale      = 1f,
            });
        }

        // Pack glyphs into atlas.
        var (atlas, placements) = AtlasPacker.Pack(
            sdfMap,
            options.AtlasWidth,
            options.AtlasHeight,
            options.AtlasPadding);

        // Build placement lookup: glyphId → (atlasX, atlasY).
        // Atlas packer uses top-left origin; Unity uses bottom-left.
        // Flip Y: atlasY_unity = atlasHeight - placementY - placementHeight
        foreach (var p in placements)
        {
            var gd = glyphTable.Find(g => g.Index == p.GlyphIndex);
            if (gd is null) continue;
            gd.AtlasX = p.X;
            // Flip Y for Unity coordinate system (origin at bottom-left of texture).
            gd.AtlasY = options.AtlasHeight - p.Y - p.Height;
        }

        // Assign deterministic GUIDs (stable for reproducible builds).
        string textureGuid   = NewGuid();
        string fontAssetGuid = NewGuid();

        var data = new FontAssetData
        {
            FaceInfo        = faceInfo,
            GlyphTable      = glyphTable,
            CharacterTable  = charTable,
            AtlasWidth      = options.AtlasWidth,
            AtlasHeight     = options.AtlasHeight,
            AtlasPadding    = options.AtlasPadding,
            AtlasRenderMode = options.AtlasRenderMode,
            AtlasTextureGuid = textureGuid,
            FontAssetGuid    = fontAssetGuid,
        };

        return (data, atlas);
    }

    private static string NewGuid() =>
        Guid.NewGuid().ToString("N");
}
