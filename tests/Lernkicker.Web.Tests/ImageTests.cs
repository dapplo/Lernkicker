using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Lernkicker.Application;
using Lernkicker.Contracts;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Web.Tests;

/// <summary>Pictures travel with quizzes and lessons, and only reach those who may see the quiz or lesson.</summary>
public sealed class ImageTests(LernkickerFactory factory) : IClassFixture<LernkickerFactory>
{
    private const string QuizFile = "other/showcase-pictures.json";
    private const string LessonFile = "math/shapes.json";

    [Fact]
    public async Task A_quiz_uploaded_with_its_pictures_shows_them_to_the_family_and_the_child()
    {
        var parent = await factory.NewFamilyAsync("pictures-quiz@example.org");
        var quiz = await LernkickerFactory.Json<QuizOverview>(await parent.PostAsync("/api/quizzes", Form(QuizFile, "showcase-pictures")));

        var sheet = await LernkickerFactory.Json<SheetDto>(await parent.GetAsync($"/api/quizzes/{quiz.QuizId}/preview"));
        var choice = Assert.IsType<MultipleChoiceSheetDto>(sheet.Exercises[0]);
        Assert.All(choice.Questions[0].Options, o => Assert.Equal("", o.Text));
        Assert.All(choice.Questions[0].Options, o => Assert.StartsWith("api/media/", o.Image!.Url));
        Assert.Equal("Kreis", choice.Questions[1].Options.Single(o => o.Image!.Alt == "Ein roter Kreis").Text);
        var exerciseImage = sheet.Exercises[1].Image!;
        Assert.Equal(("Zwei Formen", "Lernkicker, CC0"), (exerciseImage.Caption, exerciseImage.Credit));
        Assert.NotNull(Assert.IsType<MatchingSheetDto>(sheet.Exercises[2]).Rows[0].Image!.Url);

        // The same file used in several places is stored and linked once
        var circle = choice.Questions[0].Options.Single(o => o.Image!.Alt == "Ein roter Kreis").Image!.Url!;
        Assert.Equal(circle, choice.Questions[1].Options.Single(o => o.Image!.Alt == "Ein roter Kreis").Image!.Url);

        var png = await parent.GetAsync("/" + exerciseImage.Url);
        Assert.Equal(HttpStatusCode.OK, png.StatusCode);
        Assert.Equal("image/png", png.Content.Headers.ContentType!.MediaType);
        Assert.Equal(File.ReadAllBytes(Example("showcase-pictures/shapes.png")), await png.Content.ReadAsByteArrayAsync());
        Assert.Contains("immutable", png.Headers.CacheControl!.ToString());
        Assert.Contains("sandbox", png.Headers.GetValues("Content-Security-Policy").Single());

        var svg = await parent.GetAsync("/" + circle);
        Assert.Equal("image/svg+xml", svg.Content.Headers.ContentType!.MediaType);

        // The child sees the same pictures in the sheet
        var child = await TestData.CreateChildAsync(parent);
        var assignment = await TestData.AssignAsync(parent, child.Id, quiz.QuizId);
        var phone = await TestData.ChildPhoneAsync(factory, parent, child.Id);
        var childSheet = await LernkickerFactory.Json<SheetDto>(await phone.GetAsync($"/api/device/assignments/{assignment.Id}"));
        Assert.Equal(exerciseImage.Url, childSheet.Exercises[1].Image!.Url);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/" + exerciseImage.Url)).StatusCode);

        // Another family, and anyone not logged in, cannot load them
        var other = await factory.NewFamilyAsync("pictures-other@example.org");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/" + exerciseImage.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.Client().GetAsync("/" + exerciseImage.Url)).StatusCode);

        // Picture answers grade by index like text answers; the solution names the picture
        var result = await LernkickerFactory.Json<QuizResultDto>(await phone.PostAsJsonAsync(
            $"/api/device/assignments/{assignment.Id}/submit",
            new Submission(new() { ["picture-options"] = new AnswerDto { Choices = [[1], [0]] } })));
        var parts = result.Exercises[0].Parts;
        Assert.Equal((true, false), (parts[0].Correct, parts[1].Correct));
        Assert.Equal("Kreis", parts[1].Solution);
    }

    [Fact]
    public async Task A_lesson_uploaded_as_a_zip_keeps_its_pictures_and_downloads_as_a_zip_again()
    {
        var parent = await factory.NewFamilyAsync("pictures-lesson@example.org");
        var lesson = await LernkickerFactory.Json<LessonOverview>(await parent.PostAsync("/api/lessons", Zip(LessonFile, "shapes")));

        var preview = await LernkickerFactory.Json<LessonDto>(await parent.GetAsync($"/api/lessons/{lesson.LessonId}/preview"));
        Assert.Equal(["image", "text", "flashcards", "facts"], preview.Blocks.Select(b => b.Type));
        Assert.NotNull(preview.Blocks[0].Image!.Url);
        Assert.NotNull(preview.Blocks[1].Image!.Url);
        Assert.NotNull(preview.Blocks[2].Cards![0].FrontImage!.Url);
        Assert.NotNull(preview.Blocks[2].Cards![2].BackImage!.Url);

        var download = await parent.GetAsync($"/api/lessons/{lesson.LessonId}/zip");
        Assert.Equal("application/zip", download.Content.Headers.ContentType!.MediaType);
        await using var zip = new ZipArchive(await download.Content.ReadAsStreamAsync());
        Assert.Equal(
            ["shapes-with-pictures.json", "shapes/circle.svg", "shapes/shapes.png", "shapes/square.svg", "shapes/triangle.svg"],
            zip.Entries.Select(e => e.FullName).Order());

        // Deleting the lesson deletes the pictures nobody else uses
        var url = "/" + preview.Blocks[0].Image!.Url;
        Assert.Equal(HttpStatusCode.NoContent, (await parent.DeleteAsync($"/api/lessons/{lesson.LessonId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await parent.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Missing_and_unsafe_pictures_are_refused()
    {
        var parent = await factory.NewFamilyAsync("pictures-missing@example.org");

        // Without the pictures, every missing file is listed once
        var missing = await LernkickerFactory.Error(
            await parent.PostAsync("/api/quizzes", Form(QuizFile, "showcase-pictures", except: "circle.svg")), HttpStatusCode.BadRequest);
        Assert.Equal(ErrorCodes.QuizInvalid, missing.Code);
        var issue = Assert.Single(missing.Issues!);
        Assert.Equal((QuizIssueCodes.MissingImage, "exercises/0/questions/0/options/0/image/src"), (issue.Code, issue.Path));

        // A text file in place of a picture
        using var notAPicture = Form(QuizFile, "showcase-pictures", except: "shapes.png");
        notAPicture.Add(new ByteArrayContent("hello"u8.ToArray()), "files", "shapes.png");
        var wrongType = await LernkickerFactory.Error(await parent.PostAsync("/api/quizzes", notAPicture), HttpStatusCode.BadRequest);
        Assert.Equal(QuizIssueCodes.ImageType, Assert.Single(wrongType.Issues!).Code);

        // Zips with paths that leave the folder
        using var unsafeZip = new ByteArrayContent(ZipOf(("quiz.json", "{}"u8.ToArray()), ("../evil.png", [1])));
        unsafeZip.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        Assert.Equal(ErrorCodes.UploadInvalidZip, (await LernkickerFactory.Error(await parent.PostAsync("/api/quizzes", unsafeZip), HttpStatusCode.BadRequest)).Code);

        using var noJson = new ByteArrayContent(ZipOf(("rose.png", [1])));
        noJson.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        Assert.Equal(ErrorCodes.UploadNoDocument, (await LernkickerFactory.Error(await parent.PostAsync("/api/quizzes", noJson), HttpStatusCode.BadRequest)).Code);
    }

    [Fact]
    public async Task Svg_pictures_are_stored_without_scripts_or_outside_links()
    {
        var parent = await factory.NewFamilyAsync("pictures-svg@example.org");
        const string evil = """
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" onload="alert(1)">
              <script>alert(2)</script>
              <image href="https://example.org/track.png" />
              <a href="javascript:alert(3)"><rect width="10" height="10" /></a>
              <use xlink:href="https://example.org/sprite.svg#x" />
              <circle r="5" fill="url(https://example.org/x)" />
              <circle r="4" fill="#c0392b" />
            </svg>
            """;
        using var form = Form(QuizFile, "showcase-pictures", except: "circle.svg");
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(evil)), "files", "circle.svg");
        var quiz = await LernkickerFactory.Json<QuizOverview>(await parent.PostAsync("/api/quizzes", form));

        var sheet = await LernkickerFactory.Json<SheetDto>(await parent.GetAsync($"/api/quizzes/{quiz.QuizId}/preview"));
        var url = Assert.IsType<MultipleChoiceSheetDto>(sheet.Exercises[0]).Questions[0].Options.Single(o => o.Image!.Alt == "Ein roter Kreis").Image!.Url;
        var stored = await (await parent.GetAsync("/" + url)).Content.ReadAsStringAsync();

        Assert.DoesNotContain("alert", stored);
        Assert.DoesNotContain("example.org", stored);
        Assert.Contains("#c0392b", stored);
    }

    [Fact]
    public async Task Shared_pictures_reach_other_families_and_copies_keep_them()
    {
        var a = await factory.NewFamilyAsync("pictures-share-a@example.org");
        var b = await factory.NewFamilyAsync("pictures-share-b@example.org");
        var quiz = await LernkickerFactory.Json<QuizOverview>(await a.PostAsync("/api/quizzes", Zip(QuizFile, "showcase-pictures")));
        await a.PutAsJsonAsync($"/api/quizzes/{quiz.QuizId}/shared", new ShareRequest(true));

        var shared = (await LernkickerFactory.Json<List<SharedOverview>>(await b.GetAsync("/api/library"))).Single(s => s.ItemId == quiz.QuizId);
        var sheet = await LernkickerFactory.Json<SheetDto>(await b.GetAsync($"/api/library/quizzes/{shared.Id}"));
        var url = "/" + sheet.Exercises[1].Image!.Url;
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync(url)).StatusCode);

        // The copy keeps the pictures after family A stops sharing
        await LernkickerFactory.Json<QuizOverview>(await b.PostAsync($"/api/library/quizzes/{shared.Id}/copy", null));
        await a.PutAsJsonAsync($"/api/quizzes/{quiz.QuizId}/shared", new ShareRequest(false));
        var copy = await LernkickerFactory.Json<SheetDto>(await b.GetAsync($"/api/quizzes/{quiz.QuizId}/preview"));
        Assert.Equal(sheet.Exercises[1].Image!.Url, copy.Exercises[1].Image!.Url);
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync(url)).StatusCode);
    }

    private static string Example(string relative) => Path.Combine(AppContext.BaseDirectory, "quizzes", "other", relative);

    private static string ExampleFile(string file) =>
        Path.Combine(AppContext.BaseDirectory, file == LessonFile ? "lessons" : "quizzes", file);

    private static IEnumerable<string> Pictures(string file, string folder) =>
        Directory.EnumerateFiles(Path.Combine(Path.GetDirectoryName(ExampleFile(file))!, folder));

    /// <summary>The JSON and its pictures as picked in the browser: file names without folders.</summary>
    private static MultipartFormDataContent Form(string file, string folder, string? except = null)
    {
        var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(File.ReadAllBytes(ExampleFile(file))), "files", Path.GetFileName(file) },
        };
        foreach (var picture in Pictures(file, folder).Where(p => Path.GetFileName(p) != except))
        {
            form.Add(new ByteArrayContent(File.ReadAllBytes(picture)), "files", Path.GetFileName(picture));
        }

        return form;
    }

    /// <summary>The JSON and its picture folder in a zip, as one file of a form.</summary>
    private static MultipartFormDataContent Zip(string file, string folder)
    {
        var entries = Pictures(file, folder).Select(p => ($"{folder}/{Path.GetFileName(p)}", File.ReadAllBytes(p)))
            .Prepend((Path.GetFileName(file), File.ReadAllBytes(ExampleFile(file))));
        return new MultipartFormDataContent { { new ByteArrayContent(ZipOf([.. entries])), "files", "upload.zip" } };
    }

    private static byte[] ZipOf(params (string Name, byte[] Bytes)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in entries)
            {
                using var stream = zip.CreateEntry(name).Open();
                stream.Write(bytes);
            }
        }

        return buffer.ToArray();
    }
}
