using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Prepared.Client.Middleware;

namespace Prepared.Client.Tests.Middleware;

public class CorsMiddlewareTests
{
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

        var middleware = new CorsMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
        context.Response.Headers["Access-Control-Allow-Origin"].ToString().Should().Be("https://example.com");
        
        context.Response.Headers.Should().ContainKey("Access-Control-Allow-Credentials");
        context.Response.Headers["Access-Control-Allow-Credentials"].ToString().Should().Be("true");
        
        context.Response.Headers.Should().ContainKey("Access-Control-Allow-Methods");
        context.Response.Headers["Access-Control-Allow-Methods"].ToString().Should().Contain("GET");
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

        var middleware = new CorsMiddleware(next);

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
        context.Response.Body = new MemoryStream();
        
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorsMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(200);
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

        var middleware = new CorsMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }
}

