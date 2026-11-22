namespace SmsTwin.Http;

/// <summary>Error answers: the status with <c>{ "error": "..." }</c>.</summary>
internal static class ApiError
{
    public static IResult Create(int status, string message) => Results.Json(new ErrorBody(message), statusCode: status);

    public static IResult BadRequest(string message) => Create(StatusCodes.Status400BadRequest, message);
}
