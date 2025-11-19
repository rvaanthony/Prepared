using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Prepared.Client.Middleware;
using Prepared.Client.Options;

namespace Prepared.Client.Tests.Middleware;

public class CorsMiddlewareTests
{
    private static IOptions<CorsOptions> CreateOptions(string[]? allowedOrigins = null)
    {
        var options = new CorsOptions
        {
            AllowedOrigins = allowedOrigins ?? new[] { "https://example.com" },
            AllowCredentials = true,
            AllowedMethods = new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS" },
            AllowedHeaders = new[] { "Content-Type", "Authorization", "X-CSRF-TOKEN" },
            MaxAgeSeconds = 3600
        };
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    [Fact]
    public async Task InvokeAsync_WithOrigin_ShouldAddCorsHeaders()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["Origin"] = "https://example.com";
        context.Response.Body = new MemoryStream();
        
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var loggerMock = new Mock<ILogger<CorsMiddleware>>();
        var middleware = new CorsMiddleware(next, CreateOptions(), loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        // Origin is in whitelist, so headers should be added
        context.Response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
        context.Response.Headers["Access-Control-Allow-Origin"].ToString().Should().Be("https://example.com");
        
        context.Response.Headers.Should().ContainKey("Access-Control-Allow-Credentials");
        context.Response.Headers["Access-Control-Allow-Credentials"].ToString().Should().Be("true");
        
        context.Response.Headers.Should().ContainKey("Access-Control-Expose-Headers");
    }

    [Fact]
    public async Task InvokeAsync_WithoutOrigin_ShouldNotAddCorsHeaders()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var loggerMock = new Mock<ILogger<CorsMiddleware>>();
        var middleware = new CorsMiddleware(next, CreateOptions(Array.Empty<string>()), loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.Headers.Should().NotContainKey("Access-Control-Allow-Origin");
    }

    [Fact]
    public async Task InvokeAsync_WithOptionsMethod_ShouldReturn200()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "OPTIONS";
        context.Request.Headers["Origin"] = "https://example.com";
        context.Response.Body = new MemoryStream();
        
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var loggerMock = new Mock<ILogger<CorsMiddleware>>();
        var middleware = new CorsMiddleware(next, CreateOptions(), loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(204); // No Content for OPTIONS preflight
    }

    [Fact]
    public async Task InvokeAsync_ShouldCallNextMiddleware()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Response.Body = new MemoryStream();
        
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var loggerMock = new Mock<ILogger<CorsMiddleware>>();
        var middleware = new CorsMiddleware(next, CreateOptions(), loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }
}

