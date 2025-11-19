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
        var mockTranscriptHub = new Mock<ITranscriptHub>();
        // Register ITranscriptHub as scoped - use factory to return the same mock instance
        services.AddScoped<ITranscriptHub>(_ => mockTranscriptHub.Object); // Register ITranscriptHub (required by TwilioService)
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Twilio:AccountSid", "test_account_sid" },
                { "Twilio:AuthToken", "test_auth_token" },
                { "Twilio:WebhookUrl", "https://example.com" },
                { "Whisper:ApiKey", "test_whisper_key" },
                { "Whisper:Model", "whisper-1" },
                { "Whisper:Endpoint", "https://api.openai.com/v1/audio/transcriptions" },
                { "Whisper:TimeoutSeconds", "60" },
                { "OpenAI:ApiKey", "test_openai_key" },
                { "OpenAI:Endpoint", "https://api.openai.com/v1/" },
                { "OpenAI:DefaultModel", "gpt-4o-mini" },
                { "OpenAI:TimeoutSeconds", "30" }
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration); // Register IConfiguration
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Development");

        // Act
        services.AddBusinessServices(configuration, environment.Object);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var twilioService = scope.ServiceProvider.GetService<ITwilioService>();
        twilioService.Should().NotBeNull();
        twilioService.Should().BeOfType<TwilioService>();
    }

    [Fact]
    public void AddBusinessServices_ShouldRegisterMediaStreamService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Add logging services required by TwilioService and MediaStreamService
        var mockTranscriptHub = new Mock<ITranscriptHub>();
        services.AddScoped<ITranscriptHub>(_ => mockTranscriptHub.Object); // Register ITranscriptHub
        
        var mockTranscriptionService = new Mock<ITranscriptionService>();
        services.AddScoped<ITranscriptionService>(_ => mockTranscriptionService.Object);
        
        var mockSummarizationService = new Mock<ISummarizationService>();
        services.AddScoped<ISummarizationService>(_ => mockSummarizationService.Object);
        
        var mockLocationExtractionService = new Mock<ILocationExtractionService>();
        services.AddScoped<ILocationExtractionService>(_ => mockLocationExtractionService.Object);
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Twilio:AccountSid", "test_account_sid" },
                { "Twilio:AuthToken", "test_auth_token" },
                { "Twilio:WebhookUrl", "https://example.com" },
                { "Whisper:ApiKey", "test_whisper_key" },
                { "Whisper:Model", "whisper-1" },
                { "Whisper:Endpoint", "https://api.openai.com/v1/audio/transcriptions" },
                { "Whisper:TimeoutSeconds", "60" },
                { "OpenAI:ApiKey", "test_openai_key" },
                { "OpenAI:Endpoint", "https://api.openai.com/v1/" },
                { "OpenAI:DefaultModel", "gpt-4o-mini" },
                { "OpenAI:TimeoutSeconds", "30" }
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration); // Register IConfiguration
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Development");

        // Act
        services.AddBusinessServices(configuration, environment.Object);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var mediaStreamService = scope.ServiceProvider.GetService<IMediaStreamService>();
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
        var mockTranscriptHub = new Mock<ITranscriptHub>();
        // Register ITranscriptHub as scoped - use factory to return the same mock instance
        services.AddScoped<ITranscriptHub>(_ => mockTranscriptHub.Object); // Register ITranscriptHub (required by TwilioService)
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Twilio:AccountSid", "test_account_sid" },
                { "Twilio:AuthToken", "test_auth_token" },
                { "Twilio:WebhookUrl", "https://example.com" },
                { "Whisper:ApiKey", "test_whisper_key" },
                { "Whisper:Model", "whisper-1" },
                { "Whisper:Endpoint", "https://api.openai.com/v1/audio/transcriptions" },
                { "Whisper:TimeoutSeconds", "60" },
                { "OpenAI:ApiKey", "test_openai_key" },
                { "OpenAI:Endpoint", "https://api.openai.com/v1/" },
                { "OpenAI:DefaultModel", "gpt-4o-mini" },
                { "OpenAI:TimeoutSeconds", "30" }
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration); // Register IConfiguration
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Development");

        // Act
        services.AddBusinessServices(configuration, environment.Object);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var twilioService1 = scope.ServiceProvider.GetService<ITwilioService>();
        var twilioService2 = scope.ServiceProvider.GetService<ITwilioService>();
        
        // Scoped services should return the same instance within the same scope
        twilioService1.Should().NotBeNull();
        twilioService2.Should().NotBeNull();
        twilioService1.Should().BeSameAs(twilioService2);
    }
}

