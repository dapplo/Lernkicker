using Lernkicker.Application;
using Lernkicker.Contracts;

namespace Lernkicker.Web.Endpoints;

/// <summary>Turns use case results into HTTP responses. Errors carry only codes; the client translates them.</summary>
public static class ApiResults
{
    public static IResult Error(AppError error) =>
        Results.Json(
            new ApiError(error.Code, error.Issues?.Select(i => new ApiIssue(i.Code, i.Path, i.Args)).ToList()),
            ApiJson.Options,
            statusCode: error.Kind switch
            {
                ErrorKind.NotFound => StatusCodes.Status404NotFound,
                ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorKind.Locked => StatusCodes.Status423Locked,
                ErrorKind.TooLarge => StatusCodes.Status413PayloadTooLarge,
                _ => StatusCodes.Status400BadRequest,
            });

    public static IResult Error(int status, string code) => Results.Json(new ApiError(code), ApiJson.Options, statusCode: status);

    public static IResult NotFound() => Error(AppError.NotFound());

    public static IResult ToHttp(this Result result) =>
        result.IsSuccess ? Results.NoContent() : Error(result.Error!);

    public static IResult ToHttp<T>(this Result<T> result, Func<T, object> map) =>
        result.IsSuccess ? Results.Json(map(result.Value), ApiJson.Options) : Error(result.Error!);
}
