using Apps.Payload.Actions;
using Apps.Payload.Models.Requests;
using Apps.Payload.Utils;
using Newtonsoft.Json.Linq;
using Tests.Payload.Base;

namespace Tests.Payload;

[TestClass]
public class ContentActionTests : TestBase
{
    private ContentActions Actions => new(InvocationContext, FileManager);

    // ── Search ───────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SearchContent_ValidCollection_ShouldReturnAllItems()
    {
        var result = await Actions.SearchContent(new SearchContentRequest { ContentType = "posts" });

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Items.Any(), "Expected at least one post.");
        Console.WriteLine($"Total posts fetched: {result.Items.Count()}");
        PrintJsonResult(result);
    }

    [TestMethod]
    public async Task SearchContent_WithLocale_ShouldReturnItems()
    {
        var result = await Actions.SearchContent(new SearchContentRequest
        {
            ContentType = "posts",
            Locale = "en"
        });

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Items.Any());
        PrintJsonResult(result);
    }

    // ── Get ──────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetContent_ExistingPost_ShouldReturnContent()
    {
        var result = await Actions.GetContent(new GetContentRequest
        {
            ContentType = "posts",
            ContentId = "1"
        });

        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrEmpty(result.Id));
        Console.WriteLine($"Content ID: {result.Id}, Status: {result.Status}");
        PrintJsonResult(result);
    }

    // ── Download ─────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DownloadContent_ExistingPost_ShouldReturnHtmlFile()
    {
        var result = await Actions.DownloadContent(new DownloadContentRequest
        {
            ContentType = "posts",
            ContentId = "1"
        });

        Assert.IsNotNull(result);
        Assert.AreEqual("text/html", result.ContentType);

        var html = FileManager.ReadOutputAsString(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("blackbird-ucid"), "HTML should contain UCID metadata.");
        Assert.IsTrue(html.Contains("posts:1"), "UCID should encode the content type and ID.");

        Console.WriteLine($"Generated file: {result.Name}");
        Console.WriteLine(html);
    }

    [TestMethod]
    public async Task DownloadContent_WithExcludedField_ShouldOmitField()
    {
        var result = await Actions.DownloadContent(new DownloadContentRequest
        {
            ContentType = "posts",
            ContentId = "1",
            FieldsToExclude = ["title"]
        });

        var html = FileManager.ReadOutputAsString(result);
        Assert.IsFalse(html.Contains("data-json-path=\"title\""),
            "Excluded field 'title' should not appear in HTML.");
    }

    // ── Conversion round-trip (no API) ────────────────────────────────────────

    [TestMethod]
    public void DownloadThenUpload_RoundTrip_PreservesFieldValues()
    {
        var contentJson = JObject.Parse("""
            {
                "id": 42,
                "title": { "en": "Hello World", "es": "Hola Mundo" },
                "body": { "en": "Some body text", "es": "Algún texto" },
                "status": "published"
            }
            """);

        var html = JsonToHtmlConverter.Convert(contentJson, "posts", "42");

        Assert.IsTrue(html.Contains("posts:42"), "UCID must reference the content.");
        Assert.IsTrue(html.Contains("Hello World"), "English title should appear in HTML.");
        Assert.IsTrue(html.Contains("Hola Mundo"), "Spanish title should appear in HTML.");

        var parsed = HtmlToJsonConverter.Parse(html, "es");

        Assert.AreEqual("posts:42", parsed.Ucid);
        Assert.IsTrue(parsed.MainFields.ContainsKey("title"), "Parsed result should contain 'title'.");
        Assert.AreEqual("Hola Mundo", parsed.MainFields["title"]);
        Assert.AreEqual("Algún texto", parsed.MainFields["body"]);
        Assert.AreEqual(0, parsed.References.Count);
    }

    [TestMethod]
    public void LocalizableFieldDetection_WithNonLocalizedObject_ShouldNotInclude()
    {
        var contentJson = JObject.Parse("""
            {
                "id": 1,
                "meta": { "version": "1.0", "author": "admin" },
                "title": { "en": "Hello", "es": "Hola" }
            }
            """);

        var html = JsonToHtmlConverter.Convert(contentJson, "posts", "1");

        Assert.IsFalse(html.Contains("data-json-path=\"meta\""),
            "'meta' has non-locale keys and should not be treated as a localizable field.");
        Assert.IsTrue(html.Contains("data-json-path=\"title\""),
            "'title' should be included as a localizable field.");
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateContent_ValidFields_ShouldSucceed()
    {
        var result = await Actions.UpdateContent(new UpdateContentRequest
        {
            ContentType = "posts",
            ContentId = "1",
            Locale = "en",
            FieldKeys = ["title"],
            FieldValues = ["Hello from Blackbird 321"]
        });

        Assert.IsNotNull(result);
        Console.WriteLine($"Updated content ID: {result.Id}");
        PrintJsonResult(result);
    }

    // ── Upload ────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task UploadContent_FromDownloadedHtmlFile_ShouldSucceed()
    {
        // First download to generate the HTML file in the Output folder
        var downloadResult = await Actions.DownloadContent(new DownloadContentRequest
        {
            ContentType = "posts",
            ContentId = "1"
        });

        // Move the output file to the input folder for upload
        var outputPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "TestFiles", "Output", downloadResult.Name);
        var inputPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "TestFiles", "Input", downloadResult.Name);

        var absoluteOutput = Path.GetFullPath(outputPath);
        var absoluteInput = Path.GetFullPath(inputPath);

        if (File.Exists(absoluteOutput))
            File.Copy(absoluteOutput, absoluteInput, overwrite: true);

        var uploadResult = await Actions.UploadContent(new UploadContentRequest
        {
            File = downloadResult,
            TargetLocale = "es"
        });

        Assert.IsNotNull(uploadResult);
        Assert.AreEqual("posts", uploadResult.ContentType);
        Assert.IsNull(uploadResult.ErrorMessages, "No reference update errors should occur.");

        Console.WriteLine($"Uploaded to: {uploadResult.ContentType}/{uploadResult.ContentId}");
        PrintJsonResult(uploadResult);
    }
}
