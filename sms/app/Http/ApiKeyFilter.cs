namespace SmsTwin.Http;

/// <summary>
/// The send API's authentication: when <c>TWIN_API_KEY</c> is set, a request needs exactly
/// <c>Authorization: Bearer &lt;key&gt;</c>, or it gets 401 before its body is read.
/// </summary>
internal sealed class ApiKeyFilter(string? apiKey) : IEndpointFilter
{
    public static ApiKeyFilter FromEnvironment() =>
        new(Environment.GetEnvironmentVariable("TWIN_API_KEY") is { Length: > 0 } key ? key : null);

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (apiKey is not null && context.HttpContext.Request.Headers.Authorization.ToString() != $"Bearer {apiKey}")
        {
            return ValueTask.FromResult<object?>(ApiError.Create(StatusCodes.Status401Unauthorized, "missing or wrong API key"));
        }

        return next(context);
    }
}
