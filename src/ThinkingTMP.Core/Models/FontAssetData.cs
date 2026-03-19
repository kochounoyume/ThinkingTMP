namespace ThinkingTMP.Core.Models;

/// <summary>
/// Aggregates all data needed to write a TMP_FontAsset Unity .asset file.
/// </summary>
public sealed class FontAssetData
{
    public FaceInfo FaceInfo { get; init; } = null!;
    public List<GlyphData> GlyphTable { get; init; } = [];
    public List<CharacterData> CharacterTable { get; init; } = [];

    /// <summary>Width of the atlas texture in pixels.</summary>
    public int AtlasWidth { get; init; }
    /// <summary>Height of the atlas texture in pixels.</summary>
    public int AtlasHeight { get; init; }
    /// <summary>SDF padding (in atlas pixels) applied on each side of each glyph.</summary>
    public int AtlasPadding { get; init; }
    /// <summary>
    /// GlyphRenderMode value written to the .asset.
    /// 12287 = SDFAA (recommended), 8194 = SDF8.
    /// </summary>
    public int AtlasRenderMode { get; init; } = 12287;

    /// <summary>GUID of the atlas Texture2D PNG (written in the .png.meta file).</summary>
    public string AtlasTextureGuid { get; init; } = string.Empty;

    /// <summary>GUID of the font asset itself (written in the .asset.meta file).</summary>
    public string FontAssetGuid { get; init; } = string.Empty;
}
