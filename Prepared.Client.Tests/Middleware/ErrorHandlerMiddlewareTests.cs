using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Prepared.Client.Middleware;

namespace Prepared.Client.Tests.Middleware;

public class ErrorHandlerMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NoException_ShouldCallNext()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ErrorHandlerMiddleware>>();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ErrorHandlerMiddleware(next, loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_WithException_ShouldHandleError()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ErrorHandlerMiddleware>>();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new Mock<IServiceProvider>().Object;
        
        var exception = new Exception("Test exception");
        RequestDelegate next = (ctx) =>
        {
            throw exception;
        };

        var middleware = new ErrorHandlerMiddleware(next, loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(500);
        context.Response.ContentType.Should().Be("application/json");
        
        // Verify logger was called
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithException_ShouldReturnJsonError()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ErrorHandlerMiddleware>>();
        var webHostEnvironmentMock = new Mock<IWebHostEnvironment>();
        webHostEnvironmentMock.Setup(x => x.EnvironmentName).Returns("Production");
        
        var services = new ServiceCollection();
        services.AddSingleton(webHostEnvironmentMock.Object);
        var serviceProvider = services.BuildServiceProvider();
        
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = serviceProvider;
        
        RequestDelegate next = (ctx) =>
        {
            throw new Exception("Test exception");
        };

        var middleware = new ErrorHandlerMiddleware(next, loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(500);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        
        responseBody.Should().Contain("error");
        responseBody.Should().Contain("message");
    }
}

