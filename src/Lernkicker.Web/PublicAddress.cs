namespace Lernkicker.Web;

/// <summary>
/// The address people use (e.g. https://lernkicker.example.ts.net). Links in QR codes, invitations and the
/// calendar feed are built from it. <c>Lernkicker:PublicUrl</c> wins; otherwise the address from the first-run
/// setup; without either, links come from the request, which needs the proxy's X-Forwarded-Proto.
/// </summary>
public sealed class PublicAddress(Uri? configured)
{
    private volatile Uri? _stored;

    public Uri? Current => configured ?? _stored;

    public void UseStored(Uri? url) => _stored = url;
}
