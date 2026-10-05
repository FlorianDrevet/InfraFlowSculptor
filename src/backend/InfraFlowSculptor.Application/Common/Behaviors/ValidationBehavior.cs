using ErrorOr;
using FluentValidation;
using Mediator;

namespace InfraFlowSculptor.Application.Common.Behaviors;

public sealed class ValidationBehavior<TMessage, TResponse>(
    IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : IErrorOr
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(message, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            return await next(message, cancellationToken);
        }

        var errors = failures
            .Select(failure => Error.Validation(
                failure.ErrorCode,
                failure.ErrorMessage,
                new Dictionary<string, object> { ["field"] = failure.PropertyName }))
            .ToList();

        return (dynamic)errors;
    }
}
