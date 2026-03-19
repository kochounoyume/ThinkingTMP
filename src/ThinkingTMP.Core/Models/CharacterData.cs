namespace ThinkingTMP.Core.Models;

/// <summary>
/// Maps a Unicode code point to a glyph table entry, mirroring TMP_Character.
/// </summary>
public sealed class CharacterData
{
    /// <summary>Unicode code point (e.g. 0x41 for 'A').</summary>
    public uint Unicode { get; init; }

    /// <summary>Index into the GlyphTable of the font asset.</summary>
    public uint GlyphIndex { get; init; }

    public float Scale { get; init; } = 1f;
}
