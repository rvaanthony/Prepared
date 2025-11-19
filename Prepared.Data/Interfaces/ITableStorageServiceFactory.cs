namespace Prepared.Data.Interfaces;

public interface ITableStorageServiceFactory
{
    ITableStorageService Create(string connectionString);
}
