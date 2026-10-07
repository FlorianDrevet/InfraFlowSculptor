using InfraFlowSculptor.Application.Common.Behaviors;
using Mediator;
using Microsoft.Extensions.Logging;

namespace InfraFlowSculptor.Application.Tests.Observability;

public sealed class LoggingBehaviorTests
{
    [Fact]
    public async Task CommandContentNeverAppearsInLogs()
    {
        const string customerName = "Alice Martin";
        const string email = "alice@example.test";
        const string modelValue = "subscription-key-do-not-log";
        var message = new SensitiveCommand(customerName, email, modelValue);
        var logger = new CapturingLogger<LoggingBehavior<SensitiveCommand, string>>();
        var behavior = new LoggingBehavior<SensitiveCommand, string>(logger);

        var response = await behavior.Handle(
            message,
            static (_, _) => ValueTask.FromResult("handled"),
            CancellationToken.None);

        Assert.Equal("handled", response);
        var logOutput = string.Join(Environment.NewLine, logger.Values);
        Assert.DoesNotContain(customerName, logOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(email, logOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(modelValue, logOutput, StringComparison.Ordinal);
        Assert.Contains(nameof(SensitiveCommand), logOutput, StringComparison.Ordinal);
    }

    private sealed record SensitiveCommand(string CustomerName, string Email, string ModelValue) : IMessage;

    private sealed class CapturingLogger<TCategory> : ILogger<TCategory>
    {
        public List<string> Values { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Values.Add(formatter(state, exception));

            if (state is IEnumerable<KeyValuePair<string, object?>> fields)
            {
                Values.AddRange(fields.Select(field => $"{field.Key}:{field.Value}"));
            }
        }
    }
}
