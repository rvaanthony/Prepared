using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Prepared.Client.Controllers;
using Prepared.Client.Models;
using System.Diagnostics;
using Xunit;

namespace Prepared.Client.Tests.Controllers;

public class HomeControllerTests
{
    private readonly HomeController _controller;

    public HomeControllerTests()
    {
        _controller = new HomeController();
    }

    [Fact]
    public void Index_ShouldReturnView()
    {
        // Act
        var result = _controller.Index();

        // Assert
        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public void Privacy_ShouldReturnView()
    {
        // Act
        var result = _controller.Privacy();

        // Assert
        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public void Error_ShouldReturnViewWithErrorViewModel()
    {
        // Arrange
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = _controller.Error();

        // Assert
        result.Should().BeOfType<ViewResult>();
        var viewResult = result as ViewResult;
        viewResult!.Model.Should().BeOfType<ErrorViewModel>();
        var model = viewResult.Model as ErrorViewModel;
        model!.RequestId.Should().Be("test-trace-id");
    }

    [Fact]
    public void Error_WithActivityCurrent_ShouldUseActivityId()
    {
        // Arrange
        using var activity = new Activity("TestActivity");
        activity.Start();
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = _controller.Error();

        // Assert
        result.Should().BeOfType<ViewResult>();
        var viewResult = result as ViewResult;
        var model = viewResult!.Model as ErrorViewModel;
        model!.RequestId.Should().NotBeNullOrEmpty();
        
        activity.Stop();
    }
}

