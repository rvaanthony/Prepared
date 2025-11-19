using FluentAssertions;
using Prepared.Common.Models;
using Prepared.Data.Entities.v1;
using Xunit;

namespace Prepared.Data.Tests.Entities;

public class SummaryEntityTests
{
    [Fact]
    public void FromTranscriptSummary_ShouldCreateEntity()
    {
        // Arrange
        var summary = new TranscriptSummary
        {
            CallSid = "CA123",
            Summary = "Test summary",
            KeyFindings = new[] { "Finding 1", "Finding 2" },
            GeneratedAtUtc = DateTime.UtcNow
        };

        // Act
        var entity = SummaryEntity.FromTranscriptSummary(summary);

        // Assert
        entity.PartitionKey.Should().Be("ca123");
        entity.RowKey.Should().Be(SummaryEntity.RowKeyValue);
        entity.CallSid.Should().Be("CA123");
        entity.Summary.Should().Be("Test summary");
        entity.KeyFindingsJson.Should().Contain("Finding 1");
        entity.KeyFindingsJson.Should().Contain("Finding 2");
    }

    [Fact]
    public void ToTranscriptSummary_ShouldConvertBack()
    {
        // Arrange
        var entity = new SummaryEntity
        {
            PartitionKey = "ca123",
            RowKey = SummaryEntity.RowKeyValue,
            CallSid = "CA123",
            Summary = "Test summary",
            KeyFindingsJson = "[\"Finding 1\",\"Finding 2\"]",
            GeneratedAtUtc = DateTime.UtcNow
        };

        // Act
        var summary = entity.ToTranscriptSummary();

        // Assert
        summary.CallSid.Should().Be("CA123");
        summary.Summary.Should().Be("Test summary");
        summary.KeyFindings.Should().HaveCount(2);
        summary.KeyFindings[0].Should().Be("Finding 1");
        summary.KeyFindings[1].Should().Be("Finding 2");
    }

    [Fact]
    public void ToTranscriptSummary_WithInvalidJson_ShouldReturnEmptyArray()
    {
        // Arrange
        var entity = new SummaryEntity
        {
            PartitionKey = "ca123",
            RowKey = SummaryEntity.RowKeyValue,
            CallSid = "CA123",
            Summary = "Test summary",
            KeyFindingsJson = "invalid json",
            GeneratedAtUtc = DateTime.UtcNow
        };

        // Act
        var summary = entity.ToTranscriptSummary();

        // Assert
        summary.KeyFindings.Should().BeEmpty();
    }
}

