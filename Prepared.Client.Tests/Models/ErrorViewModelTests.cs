using FluentAssertions;
using Prepared.Client.Models;
using Xunit;

namespace Prepared.Client.Tests.Models;

public class ErrorViewModelTests
{
    [Fact]
    public void ShowRequestId_WithNullRequestId_ShouldReturnFalse()
    {
        // Arrange
        var model = new ErrorViewModel
        {
            RequestId = null
        };

        // Act
        var result = model.ShowRequestId;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShowRequestId_WithEmptyRequestId_ShouldReturnFalse()
    {
        // Arrange
        var model = new ErrorViewModel
        {
            RequestId = string.Empty
        };

        // Act
        var result = model.ShowRequestId;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShowRequestId_WithWhitespaceRequestId_ShouldReturnTrue()
    {
        // Arrange
        var model = new ErrorViewModel
        {
            RequestId = "   "
        };

        // Act
        var result = model.ShowRequestId;

        // Assert
        // Note: string.IsNullOrEmpty returns false for whitespace, so ShowRequestId will be true
        result.Should().BeTrue();
    }

    [Fact]
    public void ShowRequestId_WithValidRequestId_ShouldReturnTrue()
    {
        // Arrange
        var model = new ErrorViewModel
        {
            RequestId = "test-request-id"
        };

        // Act
        var result = model.ShowRequestId;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void RequestId_ShouldSetAndGet()
    {
        // Arrange
        var model = new ErrorViewModel();
        var requestId = "test-id-123";

        // Act
        model.RequestId = requestId;

        // Assert
        model.RequestId.Should().Be(requestId);
    }
}

