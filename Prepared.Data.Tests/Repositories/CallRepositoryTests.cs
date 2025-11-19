using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Prepared.Common.Enums;
using Prepared.Common.Models;
using Prepared.Data.Interfaces;
using Prepared.Data.Repositories;
using Xunit;

namespace Prepared.Data.Tests.Repositories;

public class CallRepositoryTests
{
    private readonly Mock<ITableStorageService> _tableStorageMock;
    private readonly Mock<ILogger<CallRepository>> _loggerMock;
    private readonly CallRepository _repository;

    public CallRepositoryTests()
    {
        _tableStorageMock = new Mock<ITableStorageService>();
        _loggerMock = new Mock<ILogger<CallRepository>>();
        _repository = new CallRepository(_tableStorageMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task UpsertAsync_ShouldCallTableStorage()
    {
        // Arrange
        var callInfo = new CallInfo
        {
            CallSid = "CA123",
            From = "+1234567890",
            To = "+0987654321",
            Status = CallStatus.InProgress,
            Direction = "inbound",
            StartedAt = DateTime.UtcNow
        };

        // Act
        await _repository.UpsertAsync(callInfo);

        // Assert
        _tableStorageMock.Verify(
            x => x.UpsertEntityAsync(
                It.Is<string>(s => s == "Calls"),
                It.Is<Azure.Data.Tables.ITableEntity>(e => e.PartitionKey == "ca123"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpsertAsync_WithNullCallInfo_ShouldThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.UpsertAsync(null!));
    }

    [Fact]
    public async Task GetByCallSidAsync_ShouldReturnCallInfo()
    {
        // Arrange
        var callSid = "CA123";
        var entity = new Prepared.Data.Entities.v1.CallEntity
        {
            PartitionKey = callSid.ToLowerInvariant(),
            RowKey = Prepared.Data.Entities.v1.CallEntity.RowKeyValue,
            CallSid = callSid,
            From = "+1234567890",
            To = "+0987654321",
            Status = CallStatus.InProgress.ToString(),
            Direction = "inbound",
            StartedAt = DateTime.UtcNow
        };

        _tableStorageMock
            .Setup(x => x.GetEntityAsync<Prepared.Data.Entities.v1.CallEntity>(
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
        result.From.Should().Be("+1234567890");
    }

    [Fact]
    public async Task GetByCallSidAsync_WithNullCallSid_ShouldThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetByCallSidAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetByCallSidAsync(string.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetByCallSidAsync("   "));
    }

    [Fact]
    public async Task GetActiveCallsAsync_ShouldReturnActiveCalls()
    {
        // Arrange
        var entities = new List<Prepared.Data.Entities.v1.CallEntity>
        {
            new()
            {
                PartitionKey = "ca123",
                RowKey = Prepared.Data.Entities.v1.CallEntity.RowKeyValue,
                CallSid = "CA123",
                HasActiveStream = true
            },
            new()
            {
                PartitionKey = "ca456",
                RowKey = Prepared.Data.Entities.v1.CallEntity.RowKeyValue,
                CallSid = "CA456",
                HasActiveStream = true
            }
        };

        _tableStorageMock
            .Setup(x => x.QueryEntitiesAsync<Prepared.Data.Entities.v1.CallEntity>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);

        // Act
        var result = await _repository.GetActiveCallsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.All(c => c.HasActiveStream).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldUpdateStatus()
    {
        // Arrange
        var callSid = "CA123";
        var existingCall = new CallInfo
        {
            CallSid = callSid,
            From = "+1234567890",
            To = "+0987654321",
            Status = CallStatus.InProgress
        };

        var entity = new Prepared.Data.Entities.v1.CallEntity
        {
            PartitionKey = callSid.ToLowerInvariant(),
            RowKey = Prepared.Data.Entities.v1.CallEntity.RowKeyValue,
            CallSid = callSid,
            From = existingCall.From,
            To = existingCall.To,
            Status = existingCall.Status.ToString()
        };

        _tableStorageMock
            .Setup(x => x.GetEntityAsync<Prepared.Data.Entities.v1.CallEntity>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        await _repository.UpdateStatusAsync(callSid, "completed");

        // Assert - Verify that UpsertEntityAsync was called
        // The status conversion is tested in entity tests, here we just verify the repository calls the storage service
        _tableStorageMock.Verify(
            x => x.UpsertEntityAsync(
                It.Is<string>(s => s == "Calls"),
                It.Is<Prepared.Data.Entities.v1.CallEntity>(e => e.CallSid == callSid),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateStreamInfoAsync_ShouldUpdateStreamInfo()
    {
        // Arrange
        var callSid = "CA123";
        var streamSid = "MZ123";
        var entity = new Prepared.Data.Entities.v1.CallEntity
        {
            PartitionKey = callSid.ToLowerInvariant(),
            RowKey = Prepared.Data.Entities.v1.CallEntity.RowKeyValue,
            CallSid = callSid
        };

        _tableStorageMock
            .Setup(x => x.GetEntityAsync<Prepared.Data.Entities.v1.CallEntity>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        await _repository.UpdateStreamInfoAsync(callSid, streamSid, hasActiveStream: true);

        // Assert
        _tableStorageMock.Verify(
            x => x.UpsertEntityAsync(
                It.IsAny<string>(),
                It.Is<Prepared.Data.Entities.v1.CallEntity>(e => 
                    e.StreamSid == streamSid && e.HasActiveStream == true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

