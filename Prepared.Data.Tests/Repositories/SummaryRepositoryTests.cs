using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Prepared.Common.Models;
using Prepared.Data.Interfaces;
using Prepared.Data.Repositories;
using Xunit;

namespace Prepared.Data.Tests.Repositories;

public class SummaryRepositoryTests
{
    private readonly Mock<ITableStorageService> _tableStorageMock;
    private readonly Mock<ILogger<SummaryRepository>> _loggerMock;
    private readonly SummaryRepository _repository;

    public SummaryRepositoryTests()
    {
        _tableStorageMock = new Mock<ITableStorageService>();
        _loggerMock = new Mock<ILogger<SummaryRepository>>();
        _repository = new SummaryRepository(_tableStorageMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task UpsertAsync_ShouldCallTableStorage()
    {
        // Arrange
        var summary = new TranscriptSummary
        {
            CallSid = "CA123",
            Summary = "This is a test summary",
            KeyFindings = new[] { "Finding 1", "Finding 2" },
            GeneratedAtUtc = DateTime.UtcNow
        };

        // Act
        await _repository.UpsertAsync(summary);

        // Assert
        _tableStorageMock.Verify(
            x => x.UpsertEntityAsync(
                It.Is<string>(s => s == "Summaries"),
                It.Is<Azure.Data.Tables.ITableEntity>(e => e.PartitionKey == "ca123"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpsertAsync_WithNullSummary_ShouldThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.UpsertAsync(null!));
    }

    [Fact]
    public async Task GetByCallSidAsync_ShouldReturnSummary()
    {
        // Arrange
        var callSid = "CA123";
        var entity = new Prepared.Data.Entities.v1.SummaryEntity
        {
            PartitionKey = callSid.ToLowerInvariant(),
            RowKey = Prepared.Data.Entities.v1.SummaryEntity.RowKeyValue,
            CallSid = callSid,
            Summary = "Test summary",
            KeyFindingsJson = "[\"Finding 1\",\"Finding 2\"]",
            GeneratedAtUtc = DateTime.UtcNow
        };

        _tableStorageMock
            .Setup(x => x.GetEntityAsync<Prepared.Data.Entities.v1.SummaryEntity>(
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
        result.Summary.Should().Be("Test summary");
        result.KeyFindings.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByCallSidAsync_WithNullCallSid_ShouldThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetByCallSidAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetByCallSidAsync(string.Empty));
    }
}

