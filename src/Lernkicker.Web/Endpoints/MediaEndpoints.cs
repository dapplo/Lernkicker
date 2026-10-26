using Lernkicker.Application.UseCases;
using Lernkicker.Web.Security;
using Microsoft.Net.Http.Headers;

namespace Lernkicker.Web.Endpoints;

/// <summary>The pictures of quizzes and lessons, for parents and children who may see a quiz or lesson that uses them.</summary>
public static class MediaEndpoints
{
    private const string Path = "/api/media";
    private const string PicturePolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";

    /// <summary>The link the client puts into an img element; relative, like every API path of the client.</summary>
    public static string Url(string hash) => $"{Path.TrimStart('/')}/{hash}";

    public static void MapMedia(this IEndpointRouteBuilder app)
    {
        // The hash addresses the content, so a picture never changes under its URL
        app.MapGet($"{Path}/{{hash}}", async (string hash, ImageUseCases images, HttpResponse response) =>
        {
            var image = await images.ImageAsync(hash);
            if (!image.IsSuccess)
            {
                return ApiResults.Error(image.Error!);
            }

            response.Headers.CacheControl = "private, max-age=31536000, immutable";
            response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
            // An SVG opened on its own is a document: nothing in it may run or load, even if cleaning missed something
            response.Headers.ContentSecurityPolicy = PicturePolicy;
            return Results.Bytes(image.Value.Bytes, image.Value.ContentType, entityTag: new EntityTagHeaderValue($"\"{hash}\""));
        }).RequireAuthorization(Policies.Viewer);
    }
}
