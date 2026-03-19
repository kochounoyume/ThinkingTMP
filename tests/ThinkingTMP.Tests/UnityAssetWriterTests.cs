using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ThinkingTMP.Core;
using ThinkingTMP.Core.Models;
using Xunit;

namespace ThinkingTMP.Tests;

/// <summary>
/// Integration-style tests for <see cref="UnityAssetWriter"/>.
/// These tests generate real asset files in a temp directory and verify
/// that the expected files are created with the expected content.
/// </summary>
public sealed class UnityAssetWriterTests : IDisposable
{
    private readonly string _tempDir;

    public UnityAssetWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ThinkingTMPTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    private static (FontAssetData Data, Image<L8> Atlas) MakeMinimalData()
    {
        var face = new FaceInfo
        {
            FamilyName  = "TestFont",
            StyleName   = "Regular",
            PointSize   = 90,
            Scale       = 1f,
            LineHeight  = 110,
            AscentLine  = 80,
            CapLine     = 70,
            MeanLine    = 55,
            Baseline    = 0,
            DescentLine = -20,
        };

        var glyph = new GlyphData
        {
            Index              = 65,
            MetricsWidth       = 50,
            MetricsHeight      = 60,
            HorizontalBearingX = 2,
            HorizontalBearingY = 60,
            HorizontalAdvance  = 55,
            AtlasX             = 0,
            AtlasY             = 0,
            AtlasWidth         = 68,
            AtlasHeight        = 78,
        };

        var character = new CharacterData { Unicode = 65, GlyphIndex = 65, Scale = 1f };

        var data = new FontAssetData
        {
            FaceInfo         = face,
            GlyphTable       = [glyph],
            CharacterTable   = [character],
            AtlasWidth       = 512,
            AtlasHeight      = 512,
            AtlasPadding     = 9,
            AtlasRenderMode  = 12287,
            AtlasTextureGuid = "abc123",
            FontAssetGuid    = "def456",
        };

        var atlas = new Image<L8>(512, 512);
        return (data, atlas);
    }

    [Fact]
    public void Write_CreatesExpectedFiles()
    {
        var (data, atlas) = MakeMinimalData();
        using (atlas)
            UnityAssetWriter.Write(_tempDir, "TestFont", data, atlas);

        Assert.True(File.Exists(Path.Combine(_tempDir, "TestFont SDF Atlas.png")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "TestFont SDF Atlas.png.meta")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "TestFont SDF.asset")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "TestFont SDF.asset.meta")));
    }

    [Fact]
    public void Write_AssetContainsYamlHeader()
    {
        var (data, atlas) = MakeMinimalData();
        using (atlas)
            UnityAssetWriter.Write(_tempDir, "TestFont", data, atlas);

        string assetContent = File.ReadAllText(
            Path.Combine(_tempDir, "TestFont SDF.asset"), Encoding.UTF8);

        Assert.Contains("%YAML 1.1", assetContent);
        Assert.Contains("MonoBehaviour:", assetContent);
    }

    [Fact]
    public void Write_AssetContainsTmpScriptGuid()
    {
        var (data, atlas) = MakeMinimalData();
        using (atlas)
            UnityAssetWriter.Write(_tempDir, "TestFont", data, atlas);

        string assetContent = File.ReadAllText(
            Path.Combine(_tempDir, "TestFont SDF.asset"), Encoding.UTF8);

        // TMP_FontAsset script GUID must be present for Unity to recognise the type.
        Assert.Contains("71c1514a6bd24e1e882cebbe1904ce04", assetContent);
    }

    [Fact]
    public void Write_AssetContainsFaceInfoFields()
    {
        var (data, atlas) = MakeMinimalData();
        using (atlas)
            UnityAssetWriter.Write(_tempDir, "TestFont", data, atlas);

        string content = File.ReadAllText(
            Path.Combine(_tempDir, "TestFont SDF.asset"), Encoding.UTF8);

        Assert.Contains("m_FaceInfo:", content);
        Assert.Contains("m_FamilyName: TestFont", content);
        Assert.Contains("m_PointSize:", content);
        Assert.Contains("m_AscentLine:", content);
        Assert.Contains("m_DescentLine:", content);
    }

    [Fact]
    public void Write_AssetContainsGlyphAndCharacterTables()
    {
        var (data, atlas) = MakeMinimalData();
        using (atlas)
            UnityAssetWriter.Write(_tempDir, "TestFont", data, atlas);

        string content = File.ReadAllText(
            Path.Combine(_tempDir, "TestFont SDF.asset"), Encoding.UTF8);

        Assert.Contains("m_GlyphTable:", content);
        Assert.Contains("m_CharacterTable:", content);
        Assert.Contains("m_Unicode: 65", content);
    }

    [Fact]
    public void Write_AssetContainsAtlasTextureGuid()
    {
        var (data, atlas) = MakeMinimalData();
        using (atlas)
            UnityAssetWriter.Write(_tempDir, "TestFont", data, atlas);

        string content = File.ReadAllText(
            Path.Combine(_tempDir, "TestFont SDF.asset"), Encoding.UTF8);

        Assert.Contains("abc123", content);
    }

    [Fact]
    public void Write_TextureMetaContainsGuid()
    {
        var (data, atlas) = MakeMinimalData();
        using (atlas)
            UnityAssetWriter.Write(_tempDir, "TestFont", data, atlas);

        string metaContent = File.ReadAllText(
            Path.Combine(_tempDir, "TestFont SDF Atlas.png.meta"), Encoding.UTF8);

        Assert.Contains("abc123", metaContent);
        Assert.Contains("TextureImporter:", metaContent);
    }

    [Fact]
    public void Write_FontAssetMetaContainsGuid()
    {
        var (data, atlas) = MakeMinimalData();
        using (atlas)
            UnityAssetWriter.Write(_tempDir, "TestFont", data, atlas);

        string metaContent = File.ReadAllText(
            Path.Combine(_tempDir, "TestFont SDF.asset.meta"), Encoding.UTF8);

        Assert.Contains("def456", metaContent);
        Assert.Contains("NativeFormatImporter:", metaContent);
    }

    [Fact]
    public void Write_CreatesOutputDirectory_IfAbsent()
    {
        string nestedDir = Path.Combine(_tempDir, "nested", "sub");
        var (data, atlas) = MakeMinimalData();
        using (atlas)
            UnityAssetWriter.Write(nestedDir, "TestFont", data, atlas);

        Assert.True(Directory.Exists(nestedDir));
        Assert.True(File.Exists(Path.Combine(nestedDir, "TestFont SDF.asset")));
    }
}
