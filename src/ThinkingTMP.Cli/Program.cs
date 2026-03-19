using System.CommandLine;
using System.CommandLine.Invocation;
using ThinkingTMP.Core;

// ---------------------------------------------------------------------------
// ThinkingTMP CLI
// Generates a Unity TextMeshPro-compatible SDF font asset from a TrueType /
// OpenType font file, entirely without Unity.
//
// Usage examples:
//   thinkingtmp --font MyFont.ttf --output ./out
//   thinkingtmp --font MyFont.ttf --output ./out --size 90 --padding 9
//               --atlas-width 1024 --atlas-height 1024
//               --charset "Basic Latin" "Latin-1 Supplement"
//   thinkingtmp --font MyFont.ttf --output ./out --codepoints 32-126 9728-9983
// ---------------------------------------------------------------------------

var fontOption = new Option<FileInfo>("--font", new[] { "-f" })
{
    Description = "Path to the source .ttf or .otf font file.",
    Required = true,
};

var outputOption = new Option<DirectoryInfo>("--output", new[] { "-o" })
{
    Description = "Output directory for generated asset files.",
    DefaultValueFactory = _ => new DirectoryInfo("./out"),
};

var sizeOption = new Option<int>("--size")
{
    Description = "Point size used for SDF sampling (default: 90).",
    DefaultValueFactory = _ => 90,
};

var paddingOption = new Option<int>("--padding")
{
    Description = "SDF padding in atlas pixels applied on each side of every glyph (default: 9).",
    DefaultValueFactory = _ => 9,
};

var atlasWidthOption = new Option<int>("--atlas-width")
{
    Description = "Atlas texture width in pixels; should be a power of two (default: 512).",
    DefaultValueFactory = _ => 512,
};

var atlasHeightOption = new Option<int>("--atlas-height")
{
    Description = "Atlas texture height in pixels; should be a power of two (default: 512).",
    DefaultValueFactory = _ => 512,
};

var oversampleOption = new Option<int>("--oversample")
{
    Description = "Oversampling factor for rendering before SDF generation (default: 4).",
    DefaultValueFactory = _ => 4,
};

var charsetOption = new Option<string[]>("--charset")
{
    Description =
        "Named Unicode block(s) to include. Supported values: " +
        "\"Basic Latin\", \"Latin-1 Supplement\", \"ASCII Printable\", \"ASCII\", " +
        "\"Latin Extended-A\", \"Latin Extended-B\", \"CJK Unified Ideographs\". " +
        "If omitted, ASCII Printable (U+0020\u2013U+007E) is used.",
    AllowMultipleArgumentsPerToken = true,
    Arity = ArgumentArity.ZeroOrMore,
};

var codepointsOption = new Option<string[]>("--codepoints")
{
    Description =
        "Explicit Unicode code point ranges (e.g. 32-126 0x4E00-0x9FFF). " +
        "Each token may be a single decimal/hex value or a dash-separated range.",
    AllowMultipleArgumentsPerToken = true,
    Arity = ArgumentArity.ZeroOrMore,
};

var rootCommand = new RootCommand("ThinkingTMP \u2014 Unity-independent TextMeshPro SDF font asset generator")
{
    fontOption,
    outputOption,
    sizeOption,
    paddingOption,
    atlasWidthOption,
    atlasHeightOption,
    oversampleOption,
    charsetOption,
    codepointsOption,
};

rootCommand.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
{
    var font       = parseResult.GetRequiredValue(fontOption);
    var output     = parseResult.GetValue(outputOption) ?? new DirectoryInfo("./out");
    int size       = parseResult.GetValue(sizeOption);
    int padding    = parseResult.GetValue(paddingOption);
    int atlasW     = parseResult.GetValue(atlasWidthOption);
    int atlasH     = parseResult.GetValue(atlasHeightOption);
    int oversample = parseResult.GetValue(oversampleOption);
    var charsets   = parseResult.GetValue(charsetOption);
    var cpRanges   = parseResult.GetValue(codepointsOption);

    Console.WriteLine($"[ThinkingTMP] Font     : {font.FullName}");
    Console.WriteLine($"[ThinkingTMP] Output   : {output.FullName}");
    Console.WriteLine($"[ThinkingTMP] Size     : {size}pt  Padding: {padding}  Atlas: {atlasW}\u00d7{atlasH}  Oversample: {oversample}\u00d7");

    IEnumerable<uint> codepoints = ResolveCodepoints(charsets, cpRanges);

    var opts = new FontAssetGenerator.Options
    {
        PointSize       = size,
        AtlasPadding    = padding,
        AtlasWidth      = atlasW,
        AtlasHeight     = atlasH,
        Oversample      = oversample,
    };

    Console.WriteLine("[ThinkingTMP] Generating SDF atlas\u2026");
    var (data, atlas) = FontAssetGenerator.Generate(font.FullName, codepoints, opts);

    Console.WriteLine($"[ThinkingTMP] Glyphs in atlas : {data.GlyphTable.Count}");
    Console.WriteLine($"[ThinkingTMP] Characters      : {data.CharacterTable.Count}");
    Console.WriteLine("[ThinkingTMP] Writing output files\u2026");

    string assetName = Path.GetFileNameWithoutExtension(font.Name);
    UnityAssetWriter.Write(output.FullName, assetName, data, atlas);
    atlas.Dispose();

    Console.WriteLine("[ThinkingTMP] Done.");
    Console.WriteLine($"[ThinkingTMP] Files written to: {output.FullName}");
    Console.WriteLine($"[ThinkingTMP]   {assetName} SDF Atlas.png");
    Console.WriteLine($"[ThinkingTMP]   {assetName} SDF Atlas.png.meta");
    Console.WriteLine($"[ThinkingTMP]   {assetName} SDF.asset");
    Console.WriteLine($"[ThinkingTMP]   {assetName} SDF.asset.meta");

    await Task.CompletedTask;
});

return await rootCommand.Parse(args).InvokeAsync();

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

static IEnumerable<uint> ResolveCodepoints(string[]? charsets, string[]? cpRanges)
{
    bool anyExplicit = (charsets is { Length: > 0 }) || (cpRanges is { Length: > 0 });
    var result = new HashSet<uint>();

    if (charsets is { Length: > 0 })
    {
        foreach (string cs in charsets)
        {
            var range = NamedCharset(cs);
            if (range.HasValue)
                for (uint cp = range.Value.start; cp <= range.Value.end; cp++)
                    result.Add(cp);
            else
                Console.Error.WriteLine($"[ThinkingTMP] Warning: unknown charset '{cs}', ignoring.");
        }
    }

    if (cpRanges is { Length: > 0 })
    {
        foreach (string token in cpRanges)
        {
            foreach (uint cp in ParseRange(token))
                result.Add(cp);
        }
    }

    // Default: ASCII printable U+0020\u2013U+007E
    if (!anyExplicit)
        for (uint cp = 0x0020; cp <= 0x007E; cp++)
            result.Add(cp);

    return result.Order();
}

static (uint start, uint end)? NamedCharset(string name) =>
    name.ToLowerInvariant() switch
    {
        "basic latin"            => (0x0000u, 0x007Fu),
        "latin-1 supplement"     => (0x0080u, 0x00FFu),
        "ascii printable"        => (0x0020u, 0x007Eu),
        "ascii"                  => (0x0000u, 0x007Fu),
        "latin extended-a"       => (0x0100u, 0x017Fu),
        "latin extended-b"       => (0x0180u, 0x024Fu),
        "cjk unified ideographs" => (0x4E00u, 0x9FFFu),
        _                        => null,
    };

static IEnumerable<uint> ParseRange(string token)
{
    string[] parts = token.Split('-', 2);
    if (parts.Length == 1)
    {
        if (TryParseUInt(parts[0], out uint cp))
            yield return cp;
        yield break;
    }
    if (TryParseUInt(parts[0], out uint start) && TryParseUInt(parts[1], out uint end))
        for (uint cp = start; cp <= end; cp++)
            yield return cp;
}

static bool TryParseUInt(string s, out uint result)
{
    s = s.Trim();
    if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        return uint.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null, out result);
    return uint.TryParse(s, out result);
}
