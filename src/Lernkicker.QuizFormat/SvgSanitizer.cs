using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Lernkicker.Application.Ports;

namespace Lernkicker.QuizFormat;

/// <summary>
/// Cleans an uploaded SVG with an allowlist: only drawing elements and presentation attributes in the SVG
/// namespace stay. Scripts, foreignObject, embedded images, event handlers and every reference that leaves the
/// file (a link or url() that is not "#id", @import) are removed. DTDs are not processed, so entity tricks fail.
/// </summary>
public sealed partial class SvgSanitizer : ISvgSanitizer
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";

    private static readonly HashSet<string> Elements =
    [
        "svg", "g", "defs", "symbol", "use", "title", "desc", "metadata", "style",
        "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
        "text", "tspan", "textPath",
        "linearGradient", "radialGradient", "stop", "pattern", "clipPath", "mask", "marker",
        "filter", "feBlend", "feColorMatrix", "feComponentTransfer", "feComposite", "feFlood", "feGaussianBlur",
        "feMerge", "feMergeNode", "feMorphology", "feOffset", "feDropShadow", "feFuncA", "feFuncB", "feFuncG", "feFuncR",
    ];

    private const int MaxElements = 50_000;

    public byte[]? Sanitize(byte[] bytes)
    {
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
                MaxCharactersFromEntities = 0,
                MaxCharactersInDocument = bytes.Length * 4L,
            };
            using var reader = XmlReader.Create(new MemoryStream(bytes), settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }

        if (document.Root is not { } root || root.Name != Svg + "svg" || root.DescendantsAndSelf().Count() > MaxElements)
        {
            return null;
        }

        Clean(root);
        foreach (var node in document.Nodes().Where(n => n != root).ToList())
        {
            node.Remove();
        }

        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = true }))
        {
            root.Save(writer);
        }

        return output.ToArray();
    }

    private static void Clean(XElement element)
    {
        foreach (var attribute in element.Attributes().ToList())
        {
            if (!IsAllowed(attribute))
            {
                attribute.Remove();
            }
        }

        foreach (var node in element.Nodes().ToList())
        {
            switch (node)
            {
                case XElement child when child.Name.Namespace == Svg && Elements.Contains(child.Name.LocalName):
                    Clean(child);
                    break;
                case XElement child:
                    child.Remove();
                    break;
                case XText text when element.Name.LocalName == "style" && !IsSafeCss(text.Value):
                    text.Remove();
                    break;
                case XText:
                    break;
                default:
                    // Comments and processing instructions
                    node.Remove();
                    break;
            }
        }
    }

    private static bool IsAllowed(XAttribute attribute)
    {
        if (attribute.IsNamespaceDeclaration)
        {
            return attribute.Value == Svg.NamespaceName || attribute.Value == XLink.NamespaceName;
        }

        var name = attribute.Name;
        var local = name.LocalName;
        if (name.Namespace == XLink)
        {
            return local == "href" && attribute.Value.StartsWith('#');
        }

        if (name.Namespace == XNamespace.Xml)
        {
            return local is "space" or "lang";
        }

        if (name.Namespace != XNamespace.None || local.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (local == "href")
        {
            return attribute.Value.StartsWith('#');
        }

        return IsSafeCss(attribute.Value);
    }

    /// <summary>Values (and style sheets) may only point inside the file: url(#id), no @import, no script, no CSS escapes.</summary>
    private static bool IsSafeCss(string value) =>
        !value.Contains('\\')
        && !value.Contains("@import", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("expression(", StringComparison.OrdinalIgnoreCase)
        && UrlReference().Matches(value).All(m => m.Groups["target"].Value.StartsWith('#'));

    [GeneratedRegex(@"url\s*\(\s*['""]?(?<target>[^'"")]*)", RegexOptions.IgnoreCase)]
    private static partial Regex UrlReference();
}
