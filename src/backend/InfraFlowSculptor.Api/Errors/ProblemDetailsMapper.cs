using ErrorOr;

namespace InfraFlowSculptor.Api.Errors;

public static class ProblemDetailsMapper
{
    private static readonly string[] MetadataKeys =
    [
        ProblemMetadataKeys.Permission,
        ProblemMetadataKeys.CurrentVersion,
        ProblemMetadataKeys.Current,
        ProblemMetadataKeys.Limit,
        ProblemMetadataKeys.Plan
    ];

    public static IResult ToProblem(this List<Error> errors)
    {
        var statusCode = GetStatusCode(errors);
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = GetCode(errors, statusCode)
        };

        foreach (var key in MetadataKeys)
        {
            var value = errors
                .Select(error => GetMetadataValue(error, key))
                .FirstOrDefault(metadataValue => metadataValue is not null);

            if (value is not null)
            {
                extensions[key] = value;
            }
        }

        if (statusCode == StatusCodes.Status400BadRequest)
        {
            extensions["errors"] = errors
                .GroupBy(error => GetMetadataValue(error, ProblemMetadataKeys.Field)?.ToString() ?? string.Empty)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => new
                    {
                        code = error.Code,
                        message = error.Description
                    }).ToArray(),
                    StringComparer.Ordinal);
        }

        return Results.Problem(
            statusCode: statusCode,
            title: GetTitle(statusCode),
            extensions: extensions);
    }

    private static int GetStatusCode(List<Error> errors)
    {
        if (errors.Count == 0)
        {
            return StatusCodes.Status500InternalServerError;
        }

        if (errors.Any(error => GetMetadataValue(error, ProblemMetadataKeys.Permission) is not null))
        {
            return StatusCodes.Status403Forbidden;
        }

        if (errors.Any(error => GetMetadataValue(error, ProblemMetadataKeys.CurrentVersion) is not null))
        {
            return StatusCodes.Status409Conflict;
        }

        if (errors.Any(error =>
                GetMetadataValue(error, ProblemMetadataKeys.Limit) is not null
                || GetMetadataValue(error, ProblemMetadataKeys.Plan) is not null))
        {
            return StatusCodes.Status402PaymentRequired;
        }

        if (errors.All(error => error.Type == ErrorType.Validation))
        {
            return StatusCodes.Status400BadRequest;
        }

        return errors.First().Type switch
        {
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private static string GetCode(List<Error> errors, int statusCode)
    {
        if (statusCode == StatusCodes.Status404NotFound)
        {
            return "NOT_FOUND";
        }

        if (statusCode == StatusCodes.Status403Forbidden)
        {
            return "FORBIDDEN";
        }

        if (statusCode == StatusCodes.Status402PaymentRequired)
        {
            return "PLAN_LIMIT";
        }

        if (statusCode == StatusCodes.Status409Conflict
            && errors.Any(error => GetMetadataValue(error, ProblemMetadataKeys.CurrentVersion) is not null))
        {
            return "CONFLICT_VERSION";
        }

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            return "INTERNAL";
        }

        if (errors.Count == 0)
        {
            return "INTERNAL";
        }

        var code = errors.First().Code;
        return string.IsNullOrWhiteSpace(code) ? "VALIDATION" : code;
    }

    private static object? GetMetadataValue(Error error, string key)
    {
        return error.Metadata is not null && error.Metadata.TryGetValue(key, out var value)
            ? value
            : null;
    }

    private static string GetTitle(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "One or more validation errors occurred.",
            StatusCodes.Status401Unauthorized => "Authentication is required.",
            StatusCodes.Status403Forbidden => "The requested permission is required.",
            StatusCodes.Status404NotFound => "The requested resource was not found.",
            StatusCodes.Status409Conflict => "The request conflicts with the current state.",
            StatusCodes.Status402PaymentRequired => "The plan limit has been reached.",
            _ => "An unexpected error occurred."
        };
    }
}
