using FluentAssertions;
using Prepared.Common.Enums;
using Prepared.Common.Models;
using Prepared.Data.Entities.v1;
using Xunit;

namespace Prepared.Data.Tests.Entities;

public class CallEntityTests
{
    [Fact]
    public void FromCallInfo_ShouldCreateEntity()
    {
        // Arrange
        var callInfo = new CallInfo
        {
            CallSid = "CA123",
            From = "+1234567890",
            To = "+0987654321",
            Status = CallStatus.InProgress,
            Direction = "inbound",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow.AddMinutes(5),
            Duration = 300,
            HasActiveStream = true,
            AccountSid = "AC123"
        };

        // Act
        var entity = CallEntity.FromCallInfo(callInfo);

        // Assert
        entity.PartitionKey.Should().Be("ca123");
        entity.RowKey.Should().Be(CallEntity.RowKeyValue);
        entity.CallSid.Should().Be("CA123");
        entity.From.Should().Be("+1234567890");
        entity.To.Should().Be("+0987654321");
        entity.Status.Should().Be("InProgress");
        entity.Direction.Should().Be("inbound");
        entity.HasActiveStream.Should().BeTrue();
        entity.AccountSid.Should().Be("AC123");
    }

    [Fact]
    public void ToCallInfo_ShouldConvertBack()
    {
        // Arrange
        var entity = new CallEntity
        {
            PartitionKey = "ca123",
            RowKey = CallEntity.RowKeyValue,
            CallSid = "CA123",
            From = "+1234567890",
            To = "+0987654321",
            Status = "InProgress",
            Direction = "inbound",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow.AddMinutes(5),
            Duration = 300,
            HasActiveStream = true,
            AccountSid = "AC123"
        };

        // Act
        var callInfo = entity.ToCallInfo();

        // Assert
        callInfo.CallSid.Should().Be("CA123");
        callInfo.From.Should().Be("+1234567890");
        callInfo.To.Should().Be("+0987654321");
        callInfo.Status.Should().Be(CallStatus.InProgress);
        callInfo.Direction.Should().Be("inbound");
        callInfo.HasActiveStream.Should().BeTrue();
        callInfo.AccountSid.Should().Be("AC123");
    }

    [Fact]
    public void PartitionKey_ShouldBeLowercase()
    {
        // Arrange
        var entity = new CallEntity { PartitionKey = "CA123" };

        // Assert
        entity.PartitionKey.Should().Be("ca123");
    }
}

