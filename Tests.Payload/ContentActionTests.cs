using Apps.Payload.Actions;
using Apps.Payload.Models.Requests;
using Tests.Payload.Base;

namespace Tests.Payload;

[TestClass]
public class ContentActionTests : TestBase
{
    private ContentActions Actions => new(InvocationContext, FileManager);

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

    [TestMethod]
    public async Task DownloadContent_ExistingPost_ShouldReturnHtmlFile()
    {
        var result = await Actions.DownloadContent(new DownloadContentRequest
        {
            ContentType = "posts",
            ContentId = "1",
            Locale = "en"
        });

        Assert.IsNotNull(result);
        Assert.AreEqual("text/html", result.Content.ContentType);

        var html = FileManager.ReadOutputAsString(result.Content);
        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("blackbird-ucid"), "HTML should contain UCID metadata.");
        Assert.IsTrue(html.Contains("posts:1"), "UCID should encode the content type and ID.");

        Console.WriteLine($"Generated file: {result.Content.Name}");
        Console.WriteLine(html);
    }

    [TestMethod]
    public async Task DownloadContent_WithExcludedField_ShouldOmitField()
    {
        var result = await Actions.DownloadContent(new DownloadContentRequest
        {
            ContentType = "posts",
            ContentId = "1",
            Locale = "en",
            FieldsToExclude = ["title"]
        });

        var html = FileManager.ReadOutputAsString(result.Content);
        Assert.IsFalse(html.Contains("data-json-path=\"title\""),
            "Excluded field 'title' should not appear in HTML.");
    }

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

    [TestMethod]
    public async Task UploadContent_FromDownloadedHtmlFile_ShouldSucceed()
    {
        var uploadResult = await Actions.UploadContent(new UploadContentRequest
        {
            Content = new()
            {
                Name = "posts_1.html",
                ContentType = "text/html"
            },
            Locale = "de"
        });

        Assert.IsNotNull(uploadResult);
        Assert.AreEqual("posts", uploadResult.ContentType);
        Assert.IsNull(uploadResult.ErrorMessages, "No reference update errors should occur.");

        Console.WriteLine($"Uploaded to: {uploadResult.ContentType}/{uploadResult.ContentId}");
        PrintJsonResult(uploadResult);
    }
}
