namespace ThinkingTMP.Core.Models;

/// <summary>
/// Represents font face metrics, mirroring TMP_FaceInfo.
/// All values are in pixels at the given point size.
/// </summary>
public sealed class FaceInfo
{
    public string FamilyName { get; init; } = string.Empty;
    public string StyleName { get; init; } = string.Empty;
    public float PointSize { get; init; }
    public float Scale { get; init; } = 1f;
    public float LineHeight { get; init; }
    public float AscentLine { get; init; }
    public float CapLine { get; init; }
    public float MeanLine { get; init; }
    public float Baseline { get; init; } = 0f;
    public float DescentLine { get; init; }
    public float SuperscriptOffset { get; init; }
    public float SubscriptOffset { get; init; }
    public float SuperscriptSize { get; init; } = 0.5f;
    public float SubscriptSize { get; init; } = 0.5f;
    public float UnderlineOffset { get; init; }
    public float UnderlineThickness { get; init; }
    public float StrikethroughOffset { get; init; }
    public float StrikethroughThickness { get; init; }
    public float TabWidth { get; init; }
}
