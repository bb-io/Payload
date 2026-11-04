using Apps.Payload.Utils;
using Newtonsoft.Json.Linq;

namespace Tests.Payload.Unit;

[TestClass]
public class HtmlConversionTests
{
    private static JObject LexicalWithText(string text) => JObject.Parse($$"""
        {
            "root": {
                "type": "root", "format": "", "indent": 0, "version": 1,
                "children": [
                    {
                        "type": "paragraph", "format": "", "indent": 0, "version": 1,
                        "children": [
                            { "mode": "normal", "text": "{{text}}", "type": "text", "style": "", "detail": 0, "format": 0, "version": 1 }
                        ]
                    }
                ]
            }
        }
        """);

    private static JObject ArticleWithContentTab() => new()
    {
        ["id"] = 7,
        ["title"] = new JObject { ["en"] = "Sample title" },
        ["searchText"] = new JObject { ["en"] = "Sample search text" },
        ["article"] = new JObject
        {
            ["content"] = new JObject { ["en"] = LexicalWithText("Sample body text") }
        },
        ["_status"] = "published"
    };

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

        var html = JsonToHtmlConverter.Convert(contentJson, "posts", "42", sourceLocale: "es");

        Assert.IsTrue(html.Contains("posts:42"), "UCID must reference the content.");
        Assert.IsFalse(html.Contains("Hello World"), "English title should not appear in single-locale HTML.");
        Assert.IsTrue(html.Contains("Hola Mundo"), "Spanish title should appear in HTML.");
        Assert.IsTrue(html.Contains("blackbird-locale"), "HTML should encode source locale.");

        var parsed = HtmlToJsonConverter.Parse(html);

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

        var html = JsonToHtmlConverter.Convert(contentJson, "posts", "1", sourceLocale: "en");

        Assert.IsFalse(html.Contains("data-json-path=\"meta\""),
            "'meta' has non-locale keys and should not be treated as a localizable field.");
        Assert.IsTrue(html.Contains("data-json-path=\"title\""),
            "'title' should be included as a localizable field.");
    }

    [TestMethod]
    public void Convert_LocalizedFieldInsideNamedTab_IsIncludedWithDottedPath()
    {
        var html = JsonToHtmlConverter.Convert(ArticleWithContentTab(), "articles", "7", "en");

        StringAssert.Contains(html, "data-json-path=\"article.content\"");
        StringAssert.Contains(html, "data-blackbird-key=\"articles-7-article.content\"");
        StringAssert.Contains(html, "Sample body text");
    }

    [TestMethod]
    public void Convert_LocalizedFieldsInsideArrayAndBlockRows_AreIncludedWithRowIndex()
    {
        var content = new JObject
        {
            ["id"] = 7,
            ["layout"] = new JArray
            {
                new JObject
                {
                    ["id"] = "row-1",
                    ["blockType"] = "hero",
                    ["heading"] = new JObject { ["en"] = "First heading" },
                    ["settings"] = new JObject { ["caption"] = new JObject { ["en"] = "First caption" } }
                },
                new JObject
                {
                    ["id"] = "row-2",
                    ["blockType"] = "hero",
                    ["heading"] = new JObject { ["en"] = "Second heading" }
                }
            }
        };

        var html = JsonToHtmlConverter.Convert(content, "pages", "7", "en");

        StringAssert.Contains(html, "data-json-path=\"layout.0.heading\"");
        StringAssert.Contains(html, "data-json-path=\"layout.0.settings.caption\"");
        StringAssert.Contains(html, "data-json-path=\"layout.1.heading\"");
        StringAssert.Contains(html, "Second heading");
    }

    [TestMethod]
    public void Convert_NonTranslatableNestedData_IsNotIncluded()
    {
        var content = new JObject
        {
            ["id"] = 7,
            ["body"] = LexicalWithText("Not localized body"),
            ["author"] = new JObject
            {
                ["id"] = 3,
                ["name"] = new JObject { ["en"] = "Author name" },
                ["createdAt"] = "2024-01-01T00:00:00.000Z"
            },
            ["tags"] = new JObject { ["en"] = new JArray("first", "second") },
            ["seo"] = new JObject { ["keywords"] = new JObject { ["en"] = new JArray("one", "two") } }
        };

        var html = JsonToHtmlConverter.Convert(content, "posts", "7", "en");

        Assert.IsFalse(html.Contains("data-json-path"), html);
    }

    [TestMethod]
    public void Convert_ExcludedNestedField_IsOmittedByNameOrPath()
    {
        var byName = JsonToHtmlConverter.Convert(ArticleWithContentTab(), "articles", "7", "en", ["content"]);
        var byPath = JsonToHtmlConverter.Convert(ArticleWithContentTab(), "articles", "7", "en", ["article.content"]);

        Assert.IsFalse(byName.Contains("article.content"));
        Assert.IsFalse(byPath.Contains("article.content"));
        StringAssert.Contains(byPath, "data-json-path=\"title\"");
    }

    [TestMethod]
    public void BuildPatchBody_FieldInsideNamedTab_IsNestedUnderTabName()
    {
        var html = JsonToHtmlConverter.Convert(ArticleWithContentTab(), "articles", "7", "en")
            .Replace("Sample body text", "Texto de ejemplo");
        var parsed = HtmlToJsonConverter.Parse(html);

        var body = PatchBodyBuilder.Build(parsed.MainFields, currentDocument: null, sourceDocument: null);

        Assert.AreEqual("Sample title", body["title"]?.ToString());
        var content = body["article"]?["content"] as JObject;
        Assert.IsNotNull(content, body.ToString());
        Assert.AreEqual("root", content["root"]?["type"]?.ToString());
        StringAssert.Contains(content.ToString(), "Texto de ejemplo");
        Assert.IsNull(body["article.content"]);
    }

    [TestMethod]
    public void BuildPatchBody_FieldInsideArrayRow_SendsWholeArrayWithRowIds()
    {
        var currentDocument = new JObject
        {
            ["id"] = 7,
            ["layout"] = new JArray
            {
                new JObject { ["id"] = "row-1", ["blockType"] = "hero", ["heading"] = null, ["image"] = 11 },
                new JObject { ["id"] = "row-2", ["blockType"] = "hero", ["heading"] = null, ["image"] = 12 }
            }
        };
        var fields = new Dictionary<string, object>
        {
            ["layout.0.heading"] = "Primer titular",
            ["layout.1.heading"] = "Segundo titular"
        };

        var body = PatchBodyBuilder.Build(fields, currentDocument, sourceDocument: null);

        var rows = (JArray)body["layout"]!;
        Assert.AreEqual(2, rows.Count);
        Assert.AreEqual("row-1", rows[0]["id"]?.ToString());
        Assert.AreEqual("Primer titular", rows[0]["heading"]?.ToString());
        Assert.AreEqual(11, rows[0]["image"]?.Value<int>());
        Assert.AreEqual("Segundo titular", rows[1]["heading"]?.ToString());
        Assert.IsNull(currentDocument["layout"]![0]!["heading"]!.Value<string>());
    }

    [TestMethod]
    public void ContainsArrayIndex_DetectsRowSegments()
    {
        Assert.IsTrue(PatchBodyBuilder.ContainsArrayIndex("layout.0.heading"));
        Assert.IsFalse(PatchBodyBuilder.ContainsArrayIndex("article.content"));
        Assert.IsFalse(PatchBodyBuilder.ContainsArrayIndex("title"));
    }
}
