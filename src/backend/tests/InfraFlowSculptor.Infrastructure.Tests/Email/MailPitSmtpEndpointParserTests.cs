using InfraFlowSculptor.Infrastructure.Email;

namespace InfraFlowSculptor.Infrastructure.Tests.Email;

public sealed class MailPitSmtpEndpointParserTests
{
    [Theory]
    [InlineData("endpoint=smtp://mailpit:1025", "mailpit", 1025)]
    [InlineData("endpoint=smtp://127.0.0.1:2500", "127.0.0.1", 2500)]
    public void ParseReadsTheMailPitSmtpEndpoint(string connectionString, string expectedHost, int expectedPort)
    {
        var endpoint = MailPitSmtpEndpointParser.Parse(connectionString);

        Assert.Equal(expectedHost, endpoint.Host);
        Assert.Equal(expectedPort, endpoint.Port);
    }

    [Theory]
    [InlineData("endpoint=http://mailpit:1025")]
    [InlineData("endpoint=smtp://mailpit")]
    public void ParseRejectsInvalidConnectionStrings(string connectionString)
    {
        Assert.Throws<FormatException>(() => MailPitSmtpEndpointParser.Parse(connectionString));
    }

    [Fact]
    public void ParseRejectsAnEmptyConnectionString()
    {
        Assert.Throws<ArgumentException>(() => MailPitSmtpEndpointParser.Parse(string.Empty));
    }
}
