using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using ThinkingTMP.Core.Models;

namespace ThinkingTMP.Core;

/// <summary>
/// Writes the Unity-compatible asset files that together constitute a TMP_FontAsset:
///
/// <list type="bullet">
///   <item><c>{name} SDF Atlas.png</c>        — Grayscale SDF atlas texture.</item>
///   <item><c>{name} SDF Atlas.png.meta</c>   — Unity TextureImporter meta (single channel, linear).</item>
///   <item><c>{name} SDF.asset</c>            — TMP_FontAsset in Unity YAML format.</item>
///   <item><c>{name} SDF.asset.meta</c>       — Generic Unity NativeFormatImporter meta.</item>
/// </list>
///
/// Drop all four files into a Unity project's <c>Assets/</c> folder.
/// Unity will automatically import them on the next asset refresh.
/// </summary>
public static class UnityAssetWriter
{
    // UTF-8 encoding WITHOUT BOM — Unity YAML files must not have a BOM.
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    // Script GUID for TMP_FontAsset in the TextMeshPro package.
    // This value is consistent across com.unity.textmeshpro 3.x and 4.x.
    private const string TmpFontAssetScriptGuid = "71c1514a6bd24e1e882cebbe1904ce04";

    /// <summary>
    /// Writes all four Unity asset files into <paramref name="outputDirectory"/>.
    /// </summary>
    /// <param name="outputDirectory">Destination directory (created if absent).</param>
    /// <param name="assetName">Base name used for file names (e.g. "MyFont").</param>
    /// <param name="data">Font asset data produced by <see cref="FontAssetGenerator"/>.</param>
    /// <param name="atlas">SDF atlas image produced by <see cref="FontAssetGenerator"/>.</param>
    public static void Write(
        string outputDirectory,
        string assetName,
        FontAssetData data,
        Image<L8> atlas)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ArgumentNullException.ThrowIfNull(assetName);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(atlas);

        Directory.CreateDirectory(outputDirectory);

        string texturePng  = Path.Combine(outputDirectory, $"{assetName} SDF Atlas.png");
        string textureMeta = texturePng + ".meta";
        string assetFile   = Path.Combine(outputDirectory, $"{assetName} SDF.asset");
        string assetMeta   = assetFile + ".meta";

        WriteAtlasPng(texturePng, atlas);
        WriteTextureMeta(textureMeta, data.AtlasTextureGuid, data.AtlasWidth, data.AtlasHeight);
        WriteFontAsset(assetFile, assetName, data);
        WriteFontAssetMeta(assetMeta, data.FontAssetGuid);
    }

    // -------------------------------------------------------------------------
    // Atlas PNG
    // -------------------------------------------------------------------------

    private static void WriteAtlasPng(string path, Image<L8> atlas)
    {
        atlas.Save(path, new PngEncoder
        {
            ColorType         = PngColorType.Grayscale,
            BitDepth          = PngBitDepth.Bit8,
            CompressionLevel  = PngCompressionLevel.BestCompression,
        });
    }

    // -------------------------------------------------------------------------
    // Texture .meta
    // -------------------------------------------------------------------------

    private static void WriteTextureMeta(string path, string guid, int width, int height)
    {
        int maxSize = NextPowerOfTwo(Math.Max(width, height));
        maxSize = Math.Max(maxSize, 64);

        // textureType 10 = SingleChannel, singleChannelComponent 0 = Red channel.
        // sRGBTexture 0 because SDF values are linear data, not colour.
        string yaml = $@"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1030 &103000000
TextureImporter:
  fileIDToRecycleName: {{}}
  externalObjects: {{}}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 0
    linearTexture: 1
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 1
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMasterTextureLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: {maxSize}
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 10
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: {maxSize}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    override: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
---
guid: {guid}
timeCreated: {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}
";
        File.WriteAllText(path, yaml, Utf8NoBom);
    }

    // -------------------------------------------------------------------------
    // Font Asset .asset
    // -------------------------------------------------------------------------

    private static void WriteFontAsset(string path, string assetName, FontAssetData d)
    {
        var sb = new StringBuilder(1024 * 64);

        sb.AppendLine("%YAML 1.1");
        sb.AppendLine("%TAG !u! tag:unity3d.com,2011:");
        sb.AppendLine("--- !u!114 &11400000");
        sb.AppendLine("MonoBehaviour:");
        sb.AppendLine("  m_ObjectHideFlags: 0");
        sb.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
        sb.AppendLine("  m_PrefabInstance: {fileID: 0}");
        sb.AppendLine("  m_PrefabAsset: {fileID: 0}");
        sb.AppendLine("  m_GameObject: {fileID: 0}");
        sb.AppendLine("  m_Enabled: 1");
        sb.AppendLine("  m_EditorHideFlags: 0");
        sb.AppendLine($"  m_Script: {{fileID: 11500000, guid: {TmpFontAssetScriptGuid}, type: 3}}");
        sb.AppendLine($"  m_Name: {assetName} SDF");
        sb.AppendLine("  m_EditorClassIdentifier: ");
        sb.AppendLine($"  hashCode: {ComputeHashCode(assetName)}");
        sb.AppendLine("  version: 1.1.0");

        // Face info
        var fi = d.FaceInfo;
        sb.AppendLine("  m_FaceInfo:");
        sb.AppendLine($"    m_FamilyName: {fi.FamilyName}");
        sb.AppendLine($"    m_StyleName: {fi.StyleName}");
        sb.AppendLine($"    m_PointSize: {F(fi.PointSize)}");
        sb.AppendLine($"    m_Scale: {F(fi.Scale)}");
        sb.AppendLine($"    m_LineHeight: {F(fi.LineHeight)}");
        sb.AppendLine($"    m_AscentLine: {F(fi.AscentLine)}");
        sb.AppendLine($"    m_CapLine: {F(fi.CapLine)}");
        sb.AppendLine($"    m_MeanLine: {F(fi.MeanLine)}");
        sb.AppendLine($"    m_Baseline: {F(fi.Baseline)}");
        sb.AppendLine($"    m_DescentLine: {F(fi.DescentLine)}");
        sb.AppendLine($"    m_SuperscriptOffset: {F(fi.SuperscriptOffset)}");
        sb.AppendLine($"    m_SubscriptOffset: {F(fi.SubscriptOffset)}");
        sb.AppendLine($"    m_SuperscriptSize: {F(fi.SuperscriptSize)}");
        sb.AppendLine($"    m_SubscriptSize: {F(fi.SubscriptSize)}");
        sb.AppendLine($"    m_UnderlineOffset: {F(fi.UnderlineOffset)}");
        sb.AppendLine($"    m_UnderlineThickness: {F(fi.UnderlineThickness)}");
        sb.AppendLine($"    m_StrikethroughOffset: {F(fi.StrikethroughOffset)}");
        sb.AppendLine($"    m_StrikethroughThickness: {F(fi.StrikethroughThickness)}");
        sb.AppendLine($"    m_TabWidth: {F(fi.TabWidth)}");

        // Glyph table
        sb.AppendLine("  m_GlyphTable:");
        foreach (var g in d.GlyphTable)
        {
            sb.AppendLine("  - m_Index: " + g.Index);
            sb.AppendLine("    m_Metrics:");
            sb.AppendLine($"      m_Width: {F(g.MetricsWidth)}");
            sb.AppendLine($"      m_Height: {F(g.MetricsHeight)}");
            sb.AppendLine($"      m_HorizontalBearingX: {F(g.HorizontalBearingX)}");
            sb.AppendLine($"      m_HorizontalBearingY: {F(g.HorizontalBearingY)}");
            sb.AppendLine($"      m_HorizontalAdvance: {F(g.HorizontalAdvance)}");
            sb.AppendLine("    m_GlyphRect:");
            sb.AppendLine($"      m_X: {g.AtlasX}");
            sb.AppendLine($"      m_Y: {g.AtlasY}");
            sb.AppendLine($"      m_Width: {g.AtlasWidth}");
            sb.AppendLine($"      m_Height: {g.AtlasHeight}");
            sb.AppendLine($"    m_Scale: {F(g.Scale)}");
            sb.AppendLine($"    m_AtlasIndex: {g.AtlasIndex}");
        }

        // Character table
        sb.AppendLine("  m_CharacterTable:");
        foreach (var c in d.CharacterTable)
        {
            sb.AppendLine("  - m_ElementType: 0");
            sb.AppendLine($"    m_Unicode: {c.Unicode}");
            sb.AppendLine($"    m_GlyphIndex: {c.GlyphIndex}");
            sb.AppendLine($"    m_Scale: {F(c.Scale)}");
        }

        // Atlas textures reference
        sb.AppendLine("  m_UsedGlyphRects: []");
        sb.AppendLine("  m_FreeGlyphRects: []");
        sb.AppendLine("  m_AtlasTextures:");
        sb.AppendLine($"  - {{fileID: 2800000, guid: {d.AtlasTextureGuid}, type: 3}}");
        sb.AppendLine("  m_AtlasTextureIndex: 0");
        sb.AppendLine("  m_IsMultiAtlasTexturesEnabled: 0");
        sb.AppendLine("  m_ClearDynamicDataOnBuild: 0");
        sb.AppendLine($"  m_AtlasWidth: {d.AtlasWidth}");
        sb.AppendLine($"  m_AtlasHeight: {d.AtlasHeight}");
        sb.AppendLine($"  m_AtlasPadding: {d.AtlasPadding}");
        sb.AppendLine($"  m_AtlasRenderMode: {d.AtlasRenderMode}");
        sb.AppendLine("  m_FontFeatureTable:");
        sb.AppendLine("    m_GlyphPairAdjustmentRecords: []");
        sb.AppendLine("  m_FallbackFontAssetTable: []");
        sb.AppendLine("  m_CreationSettings:");
        sb.AppendLine($"    m_SourceFontFileGUID: 0000000000000000e000000000000000");
        sb.AppendLine($"    m_PointSize: {d.FaceInfo.PointSize}");
        sb.AppendLine($"    m_PaddingValue: {d.AtlasPadding}");
        sb.AppendLine($"    m_AtlasWidth: {d.AtlasWidth}");
        sb.AppendLine($"    m_AtlasHeight: {d.AtlasHeight}");
        sb.AppendLine($"    m_CharacterSetSelectionMode: 7");
        sb.AppendLine("    m_CharacterSequence: ");
        sb.AppendLine("    m_ReferencedFontAssetGUID: 0000000000000000e000000000000000");
        sb.AppendLine("    m_ReferencedTextAssetGUID: 0000000000000000e000000000000000");
        sb.AppendLine("    m_FontStyle: 0");
        sb.AppendLine("    m_FontStyleModifier: 0");
        sb.AppendLine($"    m_RenderMode: {d.AtlasRenderMode}");
        sb.AppendLine("    m_IncludeFontFeatures: 0");
        sb.AppendLine("  m_FontAssetCreationEditorSettings:");
        sb.AppendLine($"    m_PointSize: {d.FaceInfo.PointSize}");
        sb.AppendLine($"    m_PaddingValue: {d.AtlasPadding}");
        sb.AppendLine($"    m_AtlasWidth: {d.AtlasWidth}");
        sb.AppendLine($"    m_AtlasHeight: {d.AtlasHeight}");
        sb.AppendLine("    m_CharacterSetSelectionMode: 7");
        sb.AppendLine("    m_CharacterSequence: ");
        sb.AppendLine("    m_ReferencedFontAssetGUID: 0000000000000000e000000000000000");
        sb.AppendLine("    m_ReferencedTextAssetGUID: 0000000000000000e000000000000000");
        sb.AppendLine("    m_FontStyle: 0");
        sb.AppendLine("    m_FontStyleModifier: 0");
        sb.AppendLine($"    m_RenderMode: {d.AtlasRenderMode}");
        sb.AppendLine("    m_IncludeFontFeatures: 0");
        sb.AppendLine("  m_LineBreakingRules:");
        sb.AppendLine("    m_UseModernHangulLineBreakingRules: 0");
        sb.AppendLine("    leadingCharacters: {fileID: 0}");
        sb.AppendLine("    followingCharacters: {fileID: 0}");

        File.WriteAllText(path, sb.ToString(), Utf8NoBom);
    }

    // -------------------------------------------------------------------------
    // Font Asset .meta
    // -------------------------------------------------------------------------

    private static void WriteFontAssetMeta(string path, string guid)
    {
        string yaml = $@"fileFormatVersion: 2
guid: {guid}
timeCreated: {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
";
        File.WriteAllText(path, yaml, Utf8NoBom);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Formats a float for YAML output with up to 7 significant digits.</summary>
    private static string F(float v) => v.ToString("G7", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Simple deterministic hash matching TMP's internal hashCode convention.</summary>
    private static int ComputeHashCode(string name)
    {
        unchecked
        {
            int hash = 0;
            foreach (char c in name)
                hash = hash * 31 + c;
            return hash;
        }
    }

    private static int NextPowerOfTwo(int n)
    {
        if (n <= 0) return 1;
        n--;
        n |= n >> 1; n |= n >> 2; n |= n >> 4; n |= n >> 8; n |= n >> 16;
        return n + 1;
    }
}
