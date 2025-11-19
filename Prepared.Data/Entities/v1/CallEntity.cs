using Prepared.Data.Entities.v1;
using Prepared.Common.Enums;
using Prepared.Common.Models;

namespace Prepared.Data.Entities.v1;

/// <summary>
/// Azure Table Storage entity for call records
/// PartitionKey: CallSid (lowercase)
/// RowKey: "call" (single row per call)
/// </summary>
public class CallEntity : BaseTableEntity
{
    public const string TableName = "Calls";
    public const string RowKeyValue = "call";

    public string CallSid { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? Duration { get; set; }
    public bool HasActiveStream { get; set; }
    public string? AccountSid { get; set; }
    public string? StreamSid { get; set; }

    public static CallEntity FromCallInfo(CallInfo callInfo)
    {
        return new CallEntity
        {
            PartitionKey = callInfo.CallSid.ToLowerInvariant(),
            RowKey = RowKeyValue,
            CallSid = callInfo.CallSid,
            From = callInfo.From,
            To = callInfo.To,
            Status = callInfo.Status.ToString(),
            Direction = callInfo.Direction,
            StartedAt = callInfo.StartedAt,
            CompletedAt = callInfo.CompletedAt,
            Duration = callInfo.Duration,
            HasActiveStream = callInfo.HasActiveStream,
            AccountSid = callInfo.AccountSid,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public CallInfo ToCallInfo()
    {
        return new CallInfo
        {
            CallSid = CallSid,
            From = From,
            To = To,
            Status = Enum.TryParse<CallStatus>(Status, out var status) ? status : CallStatus.Failed,
            Direction = Direction,
            StartedAt = StartedAt,
            CompletedAt = CompletedAt,
            Duration = Duration,
            HasActiveStream = HasActiveStream,
            AccountSid = AccountSid
        };
    }
}

