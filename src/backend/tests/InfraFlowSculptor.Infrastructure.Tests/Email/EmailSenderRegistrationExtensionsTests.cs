using InfraFlowSculptor.Infrastructure.Email;
using InfraFlowSculptor.Application.Common.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace InfraFlowSculptor.Infrastructure.Tests.Email;

public sealed class EmailSenderRegistrationExtensionsTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void SmtpConnectionIsRejectedOutsideDevelopmentAndTesting(string environmentName)
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddIfsEmailSender(configuration, new TestHostEnvironment(environmentName)));

        Assert.Contains("Development or Testing", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void SmtpConnectionIsAllowedInDevelopmentAndTesting(string environmentName)
    {
        var services = new ServiceCollection();

        services.AddIfsEmailSender(CreateConfiguration(), new TestHostEnvironment(environmentName));

        using var serviceProvider = services.BuildServiceProvider();
        Assert.IsType<SmtpEmailSender>(serviceProvider.GetRequiredService<IEmailSender>());
    }

    private static IConfiguration CreateConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:email"] = "endpoint=smtp://mailpit:1025",
            [EmailConfigurationKeys.SenderAddress] = "no-reply@infraflowsculptor.local"
        })
        .Build();

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "InfraFlowSculptor.Infrastructure.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
