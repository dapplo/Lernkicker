namespace Lernkicker.Web.Security;

/// <summary>
/// Security headers for every response. The Content-Security-Policy allows only our own files: no inline
/// scripts or styles, no other hosts. 'wasm-unsafe-eval' is what Blazor WebAssembly needs to run.
/// </summary>
public static class SecurityHeaders
{
    public const string Policy =
        "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self'; img-src 'self' data:; connect-src 'self'; " +
        "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(self), geolocation=(), microphone=()";
            headers.ContentSecurityPolicy = Policy;
            await next();
        });
}
