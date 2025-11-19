using Azure.Core;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Prepared.Data.Interfaces;

namespace Prepared.Data.Services;

public class TableStorageServiceFactory : ITableStorageServiceFactory
{
    private readonly ILogger<TableStorageService> _logger;

    public TableStorageServiceFactory(ILogger<TableStorageService> logger)
    {
        _logger = logger;
    }

    public ITableStorageService Create(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required", nameof(connectionString));

        var options = new TableClientOptions
        {
            Retry =
            {
                Mode = RetryMode.Exponential,
                MaxRetries = 5,
                Delay = TimeSpan.FromSeconds(2)
            }
        };

        var tableServiceClient = new TableServiceClient(connectionString, options);
        return new TableStorageService(tableServiceClient, _logger);
    }
}
