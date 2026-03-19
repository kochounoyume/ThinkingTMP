namespace ThinkingTMP.Core.Models;

/// <summary>
/// Represents a single glyph entry, mirroring TMP Glyph / TMP_GlyphMetrics / TMP_GlyphRect.
/// Metric values are in pixels at the given point size.
/// Atlas rect values are in atlas pixels (includes padding).
/// </summary>
public sealed class GlyphData
{
    /// <summary>Glyph index as reported by the font (used as glyph table key).</summary>
    public uint Index { get; init; }

    // ---- TMP_GlyphMetrics ----
    /// <summary>Glyph width in pixels (without padding).</summary>
    public float MetricsWidth { get; init; }
    /// <summary>Glyph height in pixels (without padding).</summary>
    public float MetricsHeight { get; init; }
    /// <summary>Horizontal bearing X (left side bearing) in pixels.</summary>
    public float HorizontalBearingX { get; init; }
    /// <summary>Horizontal bearing Y (distance from baseline to top of glyph) in pixels.</summary>
    public float HorizontalBearingY { get; init; }
    /// <summary>Horizontal advance width in pixels.</summary>
    public float HorizontalAdvance { get; init; }

    // ---- TMP_GlyphRect (position in atlas, including padding, Y from atlas bottom) ----
    public int AtlasX { get; set; }
    public int AtlasY { get; set; }
    public int AtlasWidth { get; init; }
    public int AtlasHeight { get; init; }

    public float Scale { get; init; } = 1f;
    public int AtlasIndex { get; init; } = 0;
}
