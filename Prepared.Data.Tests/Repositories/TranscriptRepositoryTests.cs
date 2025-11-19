using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Prepared.Common.Models;
using Prepared.Data.Interfaces;
using Prepared.Data.Repositories;
using Xunit;

namespace Prepared.Data.Tests.Repositories;

public class TranscriptRepositoryTests
{
    private readonly Mock<ITableStorageService> _tableStorageMock;
    private readonly Mock<ILogger<TranscriptRepository>> _loggerMock;
    private readonly TranscriptRepository _repository;

    public TranscriptRepositoryTests()
    {
        _tableStorageMock = new Mock<ITableStorageService>();
        _loggerMock = new Mock<ILogger<TranscriptRepository>>();
        _repository = new TranscriptRepository(_tableStorageMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task SaveAsync_ShouldCallTableStorage()
    {
        // Arrange
        var transcription = new TranscriptionResult
        {
            CallSid = "CA123",
            StreamSid = "MZ123",
            Text = "Hello, this is a test.",
            IsFinal = false,
            TimestampUtc = DateTime.UtcNow
        };

        // Act
        await _repository.SaveAsync(transcription, sequenceNumber: 1);

        // Assert
        _tableStorageMock.Verify(
            x => x.UpsertEntityAsync(
                It.Is<string>(s => s == "Transcripts"),
                It.Is<Azure.Data.Tables.ITableEntity>(e => e.PartitionKey == "ca123"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveAsync_WithNullTranscription_ShouldThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.SaveAsync(null!));
    }

    [Fact]
    public async Task GetByCallSidAsync_ShouldReturnOrderedTranscripts()
    {
        // Arrange
        var callSid = "CA123";
        var entities = new List<Prepared.Data.Entities.v1.TranscriptEntity>
        {
            new()
            {
                PartitionKey = callSid.ToLowerInvariant(),
                RowKey = DateTime.UtcNow.AddSeconds(2).Ticks.ToString("D20"),
                CallSid = callSid,
                Text = "Second",
                SequenceNumber = 2,
                TimestampUtc = DateTime.UtcNow.AddSeconds(2)
            },
            new()
            {
                PartitionKey = callSid.ToLowerInvariant(),
                RowKey = DateTime.UtcNow.Ticks.ToString("D20"),
                CallSid = callSid,
                Text = "First",
                SequenceNumber = 1,
                TimestampUtc = DateTime.UtcNow
            }
        };

        _tableStorageMock
            .Setup(x => x.QueryEntitiesAsync<Prepared.Data.Entities.v1.TranscriptEntity>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);

        // Act
        var result = await _repository.GetByCallSidAsync(callSid);

        // Assert
        result.Should().HaveCount(2);
        result[0].Text.Should().Be("First");
        result[1].Text.Should().Be("Second");
    }

    [Fact]
    public async Task GetFinalTranscriptsAsync_ShouldReturnOnlyFinalTranscripts()
    {
        // Arrange
        var callSid = "CA123";
        var entities = new List<Prepared.Data.Entities.v1.TranscriptEntity>
        {
            new()
            {
                PartitionKey = callSid.ToLowerInvariant(),
                RowKey = DateTime.UtcNow.Ticks.ToString("D20"),
                CallSid = callSid,
                Text = "Final transcript",
                IsFinal = true,
                SequenceNumber = 1
            },
            new()
            {
                PartitionKey = callSid.ToLowerInvariant(),
                RowKey = DateTime.UtcNow.AddSeconds(1).Ticks.ToString("D20"),
                CallSid = callSid,
                Text = "Interim transcript",
                IsFinal = false,
                SequenceNumber = 2
            }
        };

        _tableStorageMock
            .Setup(x => x.QueryEntitiesAsync<Prepared.Data.Entities.v1.TranscriptEntity>(
                It.IsAny<string>(),
                It.Is<string>(f => f.Contains("IsFinal eq true")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities.Where(e => e.IsFinal).ToList());

        // Act
        var result = await _repository.GetFinalTranscriptsAsync(callSid);

        // Assert
        result.Should().HaveCount(1);
        result[0].IsFinal.Should().BeTrue();
        result[0].Text.Should().Be("Final transcript");
    }

    [Fact]
    public async Task GetFullTranscriptTextAsync_ShouldConcatenateFinalTranscripts()
    {
        // Arrange
        var callSid = "CA123";
        var entities = new List<Prepared.Data.Entities.v1.TranscriptEntity>
        {
            new()
            {
                PartitionKey = callSid.ToLowerInvariant(),
                RowKey = DateTime.UtcNow.Ticks.ToString("D20"),
                CallSid = callSid,
                Text = "Hello",
                IsFinal = true,
                SequenceNumber = 1
            },
            new()
            {
                PartitionKey = callSid.ToLowerInvariant(),
                RowKey = DateTime.UtcNow.AddSeconds(1).Ticks.ToString("D20"),
                CallSid = callSid,
                Text = "world",
                IsFinal = true,
                SequenceNumber = 2
            }
        };

        _tableStorageMock
            .Setup(x => x.QueryEntitiesAsync<Prepared.Data.Entities.v1.TranscriptEntity>(
                It.IsAny<string>(),
                It.Is<string>(f => f.Contains("IsFinal eq true")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);

        // Act
        var result = await _repository.GetFullTranscriptTextAsync(callSid);

        // Assert
        result.Should().Be("Hello world");
    }
}

