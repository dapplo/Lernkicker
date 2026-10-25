using System.Text;

namespace Lernkicker.Web.Endpoints;

/// <summary>Uploads that arrive as the raw file in the request body.</summary>
public static class RequestBody
{
    /// <summary>Reads the body as UTF-8, or null when it exceeds <paramref name="maxBytes"/>.</summary>
    public static async Task<string?> ReadAsync(HttpRequest request, int maxBytes)
    {
        if (request.ContentLength > maxBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var block = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(block)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(block, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
