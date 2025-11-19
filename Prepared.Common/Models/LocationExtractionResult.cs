namespace Prepared.Common.Models;

/// <summary>
/// Represents the result of extracting a geolocation from text.
/// </summary>
public class LocationExtractionResult
{
    public string CallSid { get; init; } = string.Empty;
    public string? RawLocationText { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? FormattedAddress { get; init; }
    public double? Confidence { get; init; }
}

