using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Prepared.Common.Models;
using Prepared.Data.Interfaces;
using Prepared.Data.Repositories;
using Xunit;

namespace Prepared.Data.Tests.Repositories;

public class LocationRepositoryTests
{
    private readonly Mock<ITableStorageService> _tableStorageMock;
    private readonly Mock<ILogger<LocationRepository>> _loggerMock;
    private readonly LocationRepository _repository;

    public LocationRepositoryTests()
    {
        _tableStorageMock = new Mock<ITableStorageService>();
        _loggerMock = new Mock<ILogger<LocationRepository>>();
        _repository = new LocationRepository(_tableStorageMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task UpsertAsync_ShouldCallTableStorage()
    {
        // Arrange
        var location = new LocationExtractionResult
        {
            CallSid = "CA123",
            Latitude = 40.7128,
            Longitude = -74.0060,
            FormattedAddress = "New York, NY",
            Confidence = 0.95
        };

        // Act
        await _repository.UpsertAsync(location);

        // Assert
        _tableStorageMock.Verify(
            x => x.UpsertEntityAsync(
                It.Is<string>(s => s == "Locations"),
                It.Is<Azure.Data.Tables.ITableEntity>(e => e.PartitionKey == "ca123"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpsertAsync_WithNullLocation_ShouldThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.UpsertAsync(null!));
    }

    [Fact]
    public async Task GetByCallSidAsync_ShouldReturnLocation()
    {
        // Arrange
        var callSid = "CA123";
        var entity = new Prepared.Data.Entities.v1.LocationEntity
        {
            PartitionKey = callSid.ToLowerInvariant(),
            RowKey = Prepared.Data.Entities.v1.LocationEntity.RowKeyValue,
            CallSid = callSid,
            Latitude = 40.7128,
            Longitude = -74.0060,
            FormattedAddress = "New York, NY",
            Confidence = 0.95
        };

        _tableStorageMock
            .Setup(x => x.GetEntityAsync<Prepared.Data.Entities.v1.LocationEntity>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _repository.GetByCallSidAsync(callSid);

        // Assert
        result.Should().NotBeNull();
        result!.CallSid.Should().Be(callSid);
        result.Latitude.Should().Be(40.7128);
        result.Longitude.Should().Be(-74.0060);
        result.FormattedAddress.Should().Be("New York, NY");
    }

    [Fact]
    public async Task GetByCallSidAsync_WithNullCallSid_ShouldThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetByCallSidAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetByCallSidAsync(string.Empty));
    }
}

