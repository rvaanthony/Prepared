using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prepared.Data.Extensions;
using Xunit;

namespace Prepared.Data.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDataServices_ShouldReturnSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        var result = services.AddDataServices(configuration);

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddDataServices_ShouldNotThrowException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act & Assert
        var act = () => services.AddDataServices(configuration);
        act.Should().NotThrow();
    }

    [Fact]
    public void AddDataServices_WithNullConfiguration_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfiguration? configuration = null;

        // Act & Assert
        var act = () => services.AddDataServices(configuration!);
        act.Should().Throw<ArgumentNullException>();
    }
}

