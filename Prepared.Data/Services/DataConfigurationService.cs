using Microsoft.Extensions.Configuration;
using Prepared.Data.Interfaces;

namespace Prepared.Data.Services;

public class DataConfigurationService(IConfiguration configuration) : IDataConfigurationService
{
    public string AzureTableStorage => configuration["AzureStorage:ConnectionString"] ?? "";
}