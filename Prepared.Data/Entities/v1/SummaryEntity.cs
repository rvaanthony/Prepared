using System.Text.Json;
using Prepared.Common.Models;

namespace Prepared.Data.Entities.v1;

/// <summary>
/// Azure Table Storage entity for call summaries
/// PartitionKey: CallSid (lowercase)
/// RowKey: "summary" (single summary per call)
/// </summary>
public class SummaryEntity : BaseTableEntity
{
    public const string TableName = "Summaries";
    public const string RowKeyValue = "summary";

    public string CallSid { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string KeyFindingsJson { get; set; } = "[]"; // JSON array of strings
    public DateTime GeneratedAtUtc { get; set; }

    public static SummaryEntity FromTranscriptSummary(TranscriptSummary summary)
    {
        return new SummaryEntity
        {
            PartitionKey = summary.CallSid.ToLowerInvariant(),
            RowKey = RowKeyValue,
            CallSid = summary.CallSid,
            Summary = summary.Summary,
            KeyFindingsJson = JsonSerializer.Serialize(summary.KeyFindings),
            GeneratedAtUtc = summary.GeneratedAtUtc,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public TranscriptSummary ToTranscriptSummary()
    {
        var keyFindings = Array.Empty<string>();
        try
        {
            if (!string.IsNullOrWhiteSpace(KeyFindingsJson))
            {
                keyFindings = JsonSerializer.Deserialize<string[]>(KeyFindingsJson) ?? Array.Empty<string>();
            }
        }
        catch
        {
            // If deserialization fails, use empty array
        }

        return new TranscriptSummary
        {
            CallSid = CallSid,
            Summary = Summary,
            KeyFindings = keyFindings,
            GeneratedAtUtc = GeneratedAtUtc
        };
    }
}

