using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Prepared.Business.Extensions;
using Prepared.Business.Interfaces;
using Prepared.Business.Services;
using Xunit;

namespace Prepared.Business.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBusinessServices_ShouldRegisterTwilioService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Add logging services required by TwilioService and MediaStreamService
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Twilio:AccountSid", "test_account_sid" },
                { "Twilio:AuthToken", "test_auth_token" },
                { "Twilio:WebhookUrl", "https://example.com" }
            })
            .Build();
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Development");

        // Act
        services.AddBusinessServices(configuration, environment.Object);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var twilioService = serviceProvider.GetService<ITwilioService>();
        twilioService.Should().NotBeNull();
        twilioService.Should().BeOfType<TwilioService>();
    }

    [Fact]
    public void AddBusinessServices_ShouldRegisterMediaStreamService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Add logging services required by TwilioService and MediaStreamService
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Twilio:AccountSid", "test_account_sid" },
                { "Twilio:AuthToken", "test_auth_token" },
                { "Twilio:WebhookUrl", "https://example.com" }
            })
            .Build();
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Development");

        // Act
        services.AddBusinessServices(configuration, environment.Object);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var mediaStreamService = serviceProvider.GetService<IMediaStreamService>();
        mediaStreamService.Should().NotBeNull();
        mediaStreamService.Should().BeOfType<MediaStreamService>();
    }

    [Fact]
    public void AddBusinessServices_ShouldReturnSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Twilio:AccountSid", "test_account_sid" },
                { "Twilio:AuthToken", "test_auth_token" },
                { "Twilio:WebhookUrl", "https://example.com" }
            })
            .Build();
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Development");

        // Act
        var result = services.AddBusinessServices(configuration, environment.Object);

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddBusinessServices_ShouldRegisterServicesAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Add logging services required by TwilioService and MediaStreamService
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Twilio:AccountSid", "test_account_sid" },
                { "Twilio:AuthToken", "test_auth_token" },
                { "Twilio:WebhookUrl", "https://example.com" }
            })
            .Build();
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Development");

        // Act
        services.AddBusinessServices(configuration, environment.Object);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var twilioService1 = serviceProvider.GetService<ITwilioService>();
        var twilioService2 = serviceProvider.GetService<ITwilioService>();
        
        // Scoped services should return the same instance within the same scope
        twilioService1.Should().BeSameAs(twilioService2);
    }
}

