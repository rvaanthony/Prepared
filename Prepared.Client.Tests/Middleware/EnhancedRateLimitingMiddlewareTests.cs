using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Prepared.Client.Middleware;
using Prepared.Client.Options;

namespace Prepared.Client.Tests.Middleware;

public class EnhancedRateLimitingMiddlewareTests
{
    private static IOptions<RateLimitingOptions> CreateOptions(bool enabled = true, int maxRequests = 100, int timeWindowSeconds = 60)
    {
        var options = new RateLimitingOptions
        {
            Enabled = enabled,
            MaxRequests = maxRequests,
            TimeWindowSeconds = timeWindowSeconds
        };
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    [Fact]
    public async Task InvokeAsync_WithinRateLimit_ShouldCallNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        context.Response.Body = new MemoryStream();
        
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var loggerMock = new Mock<ILogger<EnhancedRateLimitingMiddleware>>();
        var middleware = new EnhancedRateLimitingMiddleware(next, CreateOptions(), loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_ExceedingRateLimit_ShouldReturn429()
    {
        // Arrange - Use same middleware instance to maintain rate limit cache
        RequestDelegate next = (ctx) => Task.CompletedTask;
        var loggerMock = new Mock<ILogger<EnhancedRateLimitingMiddleware>>();
        var middleware = new EnhancedRateLimitingMiddleware(next, CreateOptions(maxRequests: 100), loggerMock.Object);

        // Act - Make 101 requests (exceeding the default limit of 100)
        for (int i = 0; i < 100; i++)
        {
            var testContext = new DefaultHttpContext();
            testContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
            testContext.Response.Body = new MemoryStream();
            await middleware.InvokeAsync(testContext);
        }

        // Assert - 101st request should be rate limited
        var lastContext = new DefaultHttpContext();
        lastContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        lastContext.Response.Body = new MemoryStream();
        
        var lastNextCalled = false;
        RequestDelegate lastNext = (ctx) =>
        {
            lastNextCalled = true;
            return Task.CompletedTask;
        };
        
        // Use same middleware instance to maintain rate limit state
        await middleware.InvokeAsync(lastContext);

        lastContext.Response.StatusCode.Should().Be(429);
        lastContext.Response.Headers.Should().ContainKey("Retry-After");
        lastNextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WithUnknownIp_ShouldHandleGracefully()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = null;
        context.Response.Body = new MemoryStream();
        
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var loggerMock = new Mock<ILogger<EnhancedRateLimitingMiddleware>>();
        var middleware = new EnhancedRateLimitingMiddleware(next, CreateOptions(), loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }
}

