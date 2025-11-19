using Prepared.Common.Models;

namespace Prepared.Data.Entities.v1;

/// <summary>
/// Azure Table Storage entity for transcript chunks
/// PartitionKey: CallSid (lowercase)
/// RowKey: TimestampUtc.Ticks (for ordering and uniqueness)
/// </summary>
public class TranscriptEntity : BaseTableEntity
{
    public const string TableName = "Transcripts";

    public string CallSid { get; set; } = string.Empty;
    public string StreamSid { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsFinal { get; set; }
    public double? Confidence { get; set; }
    public DateTime TimestampUtc { get; set; }
    public int SequenceNumber { get; set; } // For ordering chunks within a call

    public static TranscriptEntity FromTranscriptionResult(TranscriptionResult result, int sequenceNumber = 0)
    {
        var ticks = result.TimestampUtc.Ticks;
        return new TranscriptEntity
        {
            PartitionKey = result.CallSid.ToLowerInvariant(),
            RowKey = ticks.ToString("D20"), // 20-digit zero-padded for proper sorting
            CallSid = result.CallSid,
            StreamSid = result.StreamSid,
            Text = result.Text,
            IsFinal = result.IsFinal,
            Confidence = result.Confidence,
            TimestampUtc = result.TimestampUtc,
            SequenceNumber = sequenceNumber,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public TranscriptionResult ToTranscriptionResult()
    {
        return new TranscriptionResult
        {
            CallSid = CallSid,
            StreamSid = StreamSid,
            Text = Text,
            IsFinal = IsFinal,
            Confidence = Confidence,
            TimestampUtc = TimestampUtc
        };
    }
}

