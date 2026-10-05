using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;

namespace InfraFlowSculptor.Application.Common.Behaviors;

public sealed partial class LoggingBehavior<TMessage, TResponse>(
    ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TMessage).Name;
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            return await next(message, cancellationToken);
        }
        finally
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                HandlerCompleted(logger, requestName, elapsedMilliseconds);
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Handled {RequestName} in {ElapsedMilliseconds} ms")]
    private static partial void HandlerCompleted(
        ILogger logger,
        string requestName,
        double elapsedMilliseconds);
}
