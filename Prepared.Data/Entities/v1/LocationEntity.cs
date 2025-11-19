using Prepared.Common.Models;

namespace Prepared.Data.Entities.v1;

/// <summary>
/// Azure Table Storage entity for extracted locations
/// PartitionKey: CallSid (lowercase)
/// RowKey: "location" (single location per call)
/// </summary>
public class LocationEntity : BaseTableEntity
{
    public const string TableName = "Locations";
    public const string RowKeyValue = "location";

    public string CallSid { get; set; } = string.Empty;
    public string? RawLocationText { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? FormattedAddress { get; set; }
    public double? Confidence { get; set; }

    public static LocationEntity FromLocationExtractionResult(LocationExtractionResult result)
    {
        return new LocationEntity
        {
            PartitionKey = result.CallSid.ToLowerInvariant(),
            RowKey = RowKeyValue,
            CallSid = result.CallSid,
            RawLocationText = result.RawLocationText,
            Latitude = result.Latitude,
            Longitude = result.Longitude,
            FormattedAddress = result.FormattedAddress,
            Confidence = result.Confidence,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public LocationExtractionResult ToLocationExtractionResult()
    {
        return new LocationExtractionResult
        {
            CallSid = CallSid,
            RawLocationText = RawLocationText,
            Latitude = Latitude,
            Longitude = Longitude,
            FormattedAddress = FormattedAddress,
            Confidence = Confidence
        };
    }
}

