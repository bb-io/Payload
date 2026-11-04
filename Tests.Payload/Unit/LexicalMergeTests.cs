using System.Text;
using System.Text.RegularExpressions;
using Apps.Payload.Utils;
using Blackbird.Filters.Extensions;
using Blackbird.Filters.Transformations;
using Newtonsoft.Json.Linq;

namespace Tests.Payload.Unit;

[TestClass]
public class LexicalMergeTests
{
    private static JObject Text(string text, int format = 0) => new()
    {
        ["type"] = "text", ["text"] = text, ["format"] = format,
        ["mode"] = "normal", ["style"] = "", ["detail"] = 0, ["version"] = 1
    };

    private static JObject Element(string type, params JObject[] children) => new()
    {
        ["type"] = type, ["format"] = "", ["indent"] = 0, ["version"] = 1, ["children"] = new JArray(children)
    };

    private static JObject InternalLink(params JObject[] children)
    {
        var link = Element("link", children);
        link["fields"] = new JObject
        {
            ["linkType"] = "internal",
            ["newTab"] = true,
            ["url"] = "https://example.com/page",
            ["doc"] = new JObject { ["relationTo"] = "pages", ["value"] = 5 }
        };
        return link;
    }

    private static JObject Upload() => new()
    {
        ["type"] = "upload", ["relationTo"] = "media", ["value"] = 11, ["version"] = 3, ["fields"] = null
    };

    private static JObject Lexical(params JObject[] children) => new() { ["root"] = Element("root", children) };

    private static JObject SampleDocumentBody() => Lexical(
        Element("paragraph", Text("Read the "), InternalLink(Text("guide")), Text(" first.", 1)),
        Upload(),
        Element("table",
            Element("tablerow",
                Element("tablecell", Element("paragraph", Text("Cell text"))))),
        Element("list",
            Element("listitem", Text("First item")),
            Element("listitem", Text("Second item"))));

    private static string Translate(string html) => html
        .Replace("Read the ", "Lee la ")
        .Replace(">guide<", ">guía<")
        .Replace(" first.", " primero.")
        .Replace("Cell text", "Texto de celda")
        .Replace("First item", "Primer elemento")
        .Replace("Second item", "Segundo elemento");

    [TestMethod]
    public void ToHtml_MarksEveryElementWithItsLexicalPath()
    {
        var html = LexicalHtmlConverter.ToHtml(SampleDocumentBody());

        StringAssert.Contains(html, "<p data-lexical-path=\"0\">");
        StringAssert.Contains(html, "data-lexical-path=\"0.1\"");
        StringAssert.Contains(html, "<p data-lexical-path=\"2.0.0.0\">");
        StringAssert.Contains(html, "<li data-lexical-path=\"3.1\">");
    }

    [TestMethod]
    public void MergeTranslation_KeepsNodesThatAreNotInTheHtml()
    {
        var original = SampleDocumentBody();
        var translatedHtml = Translate(LexicalHtmlConverter.ToHtml(original));

        var merged = LexicalHtmlConverter.MergeTranslation(original, translatedHtml);

        var children = (JArray)merged["root"]!["children"]!;
        Assert.AreEqual(4, children.Count);
        Assert.IsTrue(JToken.DeepEquals(Upload(), children[1]), children[1].ToString());
        Assert.AreEqual("table", children[2]["type"]?.ToString());
        Assert.AreEqual("Texto de celda", children[2].SelectToken("children[0].children[0].children[0].children[0].text")?.ToString());
        Assert.AreEqual("Segundo elemento", children[3].SelectToken("children[1].children[0].text")?.ToString());
    }

    [TestMethod]
    public void MergeTranslation_KeepsLinkFieldsAndTextFormat()
    {
        var original = SampleDocumentBody();
        var translatedHtml = Translate(LexicalHtmlConverter.ToHtml(original));

        var merged = LexicalHtmlConverter.MergeTranslation(original, translatedHtml);

        var paragraph = merged["root"]!["children"]![0]!;
        Assert.AreEqual("Lee la ", paragraph["children"]![0]!["text"]?.ToString());
        var link = paragraph["children"]![1]!;
        Assert.AreEqual("internal", link["fields"]?["linkType"]?.ToString());
        Assert.AreEqual(5, link["fields"]?["doc"]?["value"]?.Value<int>());
        Assert.AreEqual("guía", link["children"]![0]!["text"]?.ToString());
        Assert.AreEqual(" primero.", paragraph["children"]![2]!["text"]?.ToString());
        Assert.AreEqual(1, paragraph["children"]![2]!["format"]?.Value<int>());
    }

    [TestMethod]
    public void MergeTranslation_HtmlWithoutPaths_IsConvertedAsBefore()
    {
        var merged = LexicalHtmlConverter.MergeTranslation(SampleDocumentBody(), "<p>Texto nuevo</p>");

        var children = (JArray)merged["root"]!["children"]!;
        Assert.AreEqual(1, children.Count);
        Assert.AreEqual("Texto nuevo", children[0]["children"]![0]!["text"]?.ToString());
    }

    [TestMethod]
    public void UploadRoundTrip_FromTranslatedXliff_ReadsTargetHtmlAndMergesRichText()
    {
        var allLocales = new JObject
        {
            ["id"] = 7,
            ["article"] = new JObject { ["content"] = new JObject { ["en"] = SampleDocumentBody() } }
        };
        var html = JsonToHtmlConverter.Convert(allLocales, "articles", "7", "en");
        var xliff = Transformation.Load(new MemoryStream(Encoding.UTF8.GetBytes(html)), "articles_7.html")
            .Value.ToStream().ReadString();
        var translatedXliff = Regex.Replace(xliff, "<source>(.*?)</source>",
            match => $"{match.Value}<target>{Translate(match.Groups[1].Value)}</target>");

        var restoredHtml = TranslatedFileReader.ReadHtml(Encoding.UTF8.GetBytes(translatedXliff), "articles_7.html.xlf");
        var parsed = HtmlToJsonConverter.Parse(restoredHtml);
        var sourceDocument = new JObject { ["article"] = new JObject { ["content"] = SampleDocumentBody() } };
        var body = PatchBodyBuilder.Build(parsed.MainFields, currentDocument: null, sourceDocument);

        Assert.AreEqual("articles:7", parsed.Ucid);
        var children = (JArray)body["article"]!["content"]!["root"]!["children"]!;
        Assert.IsTrue(JToken.DeepEquals(Upload(), children[1]), body.ToString());
        var link = children[0]["children"]![1]!;
        Assert.AreEqual("internal", link["fields"]?["linkType"]?.ToString());
        Assert.AreEqual("guía", link["children"]![0]!["text"]?.ToString());
        Assert.AreEqual("Segundo elemento", children[3].SelectToken("children[1].children[0].text")?.ToString());
    }

    [TestMethod]
    public void UploadRoundTrip_RichTextInsideTab_IsMergedWithSourceDocument()
    {
        var sourceDocument = new JObject
        {
            ["id"] = 7,
            ["article"] = new JObject { ["content"] = SampleDocumentBody() }
        };
        var allLocales = new JObject
        {
            ["id"] = 7,
            ["article"] = new JObject { ["content"] = new JObject { ["en"] = SampleDocumentBody() } }
        };
        var translatedFile = Translate(JsonToHtmlConverter.Convert(allLocales, "articles", "7", "en"));
        var parsed = HtmlToJsonConverter.Parse(translatedFile);

        var body = PatchBodyBuilder.Build(parsed.MainFields, currentDocument: null, sourceDocument);

        Assert.AreEqual("en", parsed.SourceLocale);
        var children = (JArray)body["article"]!["content"]!["root"]!["children"]!;
        Assert.IsTrue(JToken.DeepEquals(Upload(), children[1]), body.ToString());
        Assert.AreEqual("guía", children[0]["children"]![1]!["children"]![0]!["text"]?.ToString());
    }
}
