using Azure;
using Azure.Data.Tables;

namespace Prepared.Data.Entities.v1;

public abstract class BaseTableEntity : ITableEntity
{
    private string _partitionKey = string.Empty;
    private string _rowKey = string.Empty;

    public virtual string EntityName => GetType().Name;
    public virtual int Version => 1;

    public string PartitionKey
    {
        get => _partitionKey;
        set => _partitionKey = value?.ToLowerInvariant() ?? string.Empty;
    }

    public string RowKey
    {
        get => _rowKey;
        set => _rowKey = value?.ToLowerInvariant() ?? string.Empty;
    }

    public DateTimeOffset? Timestamp { get; set; } // Required by ITableEntity
    public ETag ETag { get; set; } // Required by ITableEntity
}