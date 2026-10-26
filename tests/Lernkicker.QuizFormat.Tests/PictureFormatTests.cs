using System.Text;
using Lernkicker.Domain.Media;
using Lernkicker.Domain.Quizzes;
using Lernkicker.Tests;

namespace Lernkicker.QuizFormat.Tests;

/// <summary>Pictures in quiz/v1 and lesson/v1, and the cleaning of uploaded SVG files.</summary>
public class PictureFormatTests
{
    [Fact]
    public void Picture_quiz_lists_every_picture_with_its_path()
    {
        var result = new QuizJson().Parse(File.ReadAllText(Examples.PathOf("other/showcase-pictures.json")));
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));

        var uses = result.Quiz!.Images().ToList();
        Assert.Equal("exercises/0/questions/0/options/0/image", uses[0].Path);
        Assert.Contains(uses, u => u.Path == "exercises/1/image" && u.Image.Caption == "Zwei Formen");
        Assert.Contains(uses, u => u.Path == "exercises/2/rows/1/image");
        var choice = Assert.IsType<MultipleChoiceExercise>(result.Quiz.Exercises[0]);
        Assert.Equal((null, "Ein grünes Dreieck"), (choice.Questions[0].Options[1].Text, choice.Questions[0].Options[1].Label));
    }

    [Fact]
    public void Picture_options_read_back_from_the_stored_source()
    {
        var format = new QuizJson();
        var quiz = format.Read(File.ReadAllText(Examples.PathOf("other/showcase-pictures.json")));
        var option = Assert.IsType<MultipleChoiceExercise>(quiz.Exercises[0]).Questions[1].Options[1];

        Assert.Equal(new ChoiceOption("Kreis", new ImageRef("showcase-pictures/circle.svg", "Ein roter Kreis")), option);
    }

    [Theory]
    [InlineData("https://example.org/rose.png")]
    [InlineData("/etc/passwd")]
    [InlineData("../rose.png")]
    [InlineData("pictures/../../rose.png")]
    [InlineData("C:\\rose.png")]
    public void Picture_paths_stay_inside_the_upload(string src)
    {
        var json = $$"""
            { "format": "quiz/v1", "id": "paths", "title": "T", "subject": "other", "gradeLevel": 4,
              "exercises": [ { "id": "a", "type": "ordering", "instruction": "I", "steps": ["1", "2"],
                               "image": { "src": {{System.Text.Json.JsonSerializer.Serialize(src)}}, "alt": "A" } } ] }
            """;

        var result = new QuizJson().Parse(json);

        Assert.Contains(result.Issues, i => i.Code == QuizIssueCodes.Schema && i.Path == "exercises/0/image/src");
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, ImageFormats.Png)]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, ImageFormats.Jpeg)]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, ImageFormats.Gif)]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, ImageFormats.WebP)]
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 }, null)]
    [InlineData(new byte[] { }, null)]
    public void Raster_formats_are_recognised_by_their_first_bytes(byte[] bytes, string? expected) =>
        Assert.Equal(expected, ImageFormats.Detect(bytes));

    [Fact]
    public void Svg_keeps_the_drawing_and_loses_everything_active()
    {
        const string svg = """
            <?xml version="1.0"?>
            <!-- made with an editor -->
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:inkscape="http://www.inkscape.org/namespaces/inkscape" viewBox="0 0 10 10" onclick="x()">
              <style>@import url(https://example.org/a.css); .a { fill: red }</style>
              <defs><linearGradient id="g"><stop offset="0" stop-color="#fff" /></linearGradient></defs>
              <foreignObject><div xmlns="http://www.w3.org/1999/xhtml">hi</div></foreignObject>
              <rect class="a" width="5" height="5" fill="url(#g)" inkscape:label="box" style="stroke: url('https://example.org/s')" />
              <animate attributeName="href" to="javascript:x()" />
              <text x="1" y="9">Kreis</text>
            </svg>
            """;

        var clean = Encoding.UTF8.GetString(new SvgSanitizer().Sanitize(Encoding.UTF8.GetBytes(svg))!);

        Assert.StartsWith("<svg", clean);
        Assert.Contains("fill=\"url(#g)\"", clean);
        Assert.Contains("<linearGradient id=\"g\">", clean);
        Assert.Contains(">Kreis</text>", clean);
        Assert.DoesNotContain("example.org", clean);
        Assert.DoesNotContain("onclick", clean);
        Assert.DoesNotContain("foreignObject", clean);
        Assert.DoesNotContain("animate", clean);
        Assert.DoesNotContain("inkscape", clean);
        Assert.DoesNotContain("editor", clean);
    }

    [Theory]
    [InlineData("""<!DOCTYPE svg [<!ENTITY a "aaaaaaaaaa">]><svg xmlns="http://www.w3.org/2000/svg"><text>&a;</text></svg>""")]
    [InlineData("""<!DOCTYPE svg [<!ENTITY x SYSTEM "file:///etc/passwd">]><svg xmlns="http://www.w3.org/2000/svg"><text>&x;</text></svg>""")]
    [InlineData("""<html><body>not a picture</body></html>""")]
    [InlineData("""<svg>no namespace</svg>""")]
    [InlineData("just text")]
    public void Svg_that_cannot_be_read_safely_is_refused(string svg) =>
        Assert.Null(new SvgSanitizer().Sanitize(Encoding.UTF8.GetBytes(svg)));
}
