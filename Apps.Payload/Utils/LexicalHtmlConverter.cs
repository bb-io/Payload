using System.Text;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Apps.Payload.Utils;

public static class LexicalHtmlConverter
{
    private const int Bold = 1;
    private const int Italic = 2;
    private const int Strikethrough = 4;
    private const int Underline = 8;
    private const int Code = 16;

    #region ToHtml

    public static string ToHtml(JToken lexicalRoot)
    {
        var root = lexicalRoot["root"] ?? lexicalRoot;
        var sb = new StringBuilder();
        AppendChildren(root["children"], sb);
        return sb.ToString();
    }

    private static void AppendChildren(JToken? children, StringBuilder sb)
    {
        if (children == null) return;
        foreach (var node in children)
            AppendNode(node, sb);
    }

    private static void AppendNode(JToken node, StringBuilder sb)
    {
        switch (node["type"]?.ToString())
        {
            case "paragraph":
                sb.Append("<p>");
                AppendChildren(node["children"], sb);
                sb.Append("</p>");
                break;

            case "heading":
                var tag = node["tag"]?.ToString() ?? "h2";
                sb.Append($"<{tag}>");
                AppendChildren(node["children"], sb);
                sb.Append($"</{tag}>");
                break;

            case "quote":
                sb.Append("<blockquote>");
                AppendChildren(node["children"], sb);
                sb.Append("</blockquote>");
                break;

            case "list":
                var listTag = node["listType"]?.ToString() == "number" ? "ol" : "ul";
                sb.Append($"<{listTag}>");
                AppendChildren(node["children"], sb);
                sb.Append($"</{listTag}>");
                break;

            case "listitem":
                sb.Append("<li>");
                AppendChildren(node["children"], sb);
                sb.Append("</li>");
                break;

            case "link":
            case "autolink":
                var url = node["fields"]?["url"]?.ToString() ?? node["url"]?.ToString() ?? "#";
                sb.Append($"<a href=\"{HtmlEntity.Entitize(url)}\">");
                AppendChildren(node["children"], sb);
                sb.Append("</a>");
                break;

            case "linebreak":
                sb.Append("<br>");
                break;

            case "text":
                var text = node["text"]?.ToString() ?? string.Empty;
                var format = node["format"]?.Value<int>() ?? 0;
                var escaped = HtmlEntity.Entitize(text);
                if ((format & Code) != 0) escaped = $"<code>{escaped}</code>";
                if ((format & Bold) != 0) escaped = $"<strong>{escaped}</strong>";
                if ((format & Italic) != 0) escaped = $"<em>{escaped}</em>";
                if ((format & Underline) != 0) escaped = $"<u>{escaped}</u>";
                if ((format & Strikethrough) != 0) escaped = $"<s>{escaped}</s>";
                sb.Append(escaped);
                break;

            default:
                AppendChildren(node["children"], sb);
                break;
        }
    }

    #endregion

    #region FromHtml

    public static JObject FromHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        var children = new JArray();

        foreach (var child in body.ChildNodes)
        {
            foreach (var node in ConvertHtmlNode(child))
                children.Add(node);
        }

        return new JObject
        {
            ["root"] = new JObject
            {
                ["type"] = "root",
                ["format"] = "",
                ["indent"] = 0,
                ["version"] = 1,
                ["children"] = children,
                ["direction"] = "ltr"
            }
        };
    }

    private static IEnumerable<JObject> ConvertHtmlNode(HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText);
            if (!string.IsNullOrWhiteSpace(text))
                yield return MakeParagraph([MakeTextNode(text, 0)]);
            yield break;
        }

        if (node.NodeType != HtmlNodeType.Element) yield break;

        switch (node.Name.ToLowerInvariant())
        {
            case "p":
                yield return MakeParagraph(ConvertInlineNodes(node.ChildNodes));
                break;
            case "h1": case "h2": case "h3": case "h4": case "h5": case "h6":
                yield return MakeHeading(node.Name.ToLowerInvariant(), ConvertInlineNodes(node.ChildNodes));
                break;
            case "blockquote":
                yield return MakeQuote(ConvertInlineNodes(node.ChildNodes));
                break;
            case "ul":
                yield return MakeList("bullet", ConvertListItems(node));
                break;
            case "ol":
                yield return MakeList("number", ConvertListItems(node));
                break;
            case "br":
                yield return MakeParagraph([]);
                break;
            default:
                var inlines = ConvertInlineNodes(node.ChildNodes).ToList();
                if (inlines.Count > 0)
                    yield return MakeParagraph(inlines);
                break;
        }
    }

    private static IEnumerable<JObject> ConvertListItems(HtmlNode listNode) =>
        listNode.ChildNodes
            .Where(n => n.Name == "li")
            .Select(li => MakeListItem(ConvertInlineNodes(li.ChildNodes)));

    private static IEnumerable<JObject> ConvertInlineNodes(HtmlNodeCollection nodes)
    {
        foreach (var node in nodes)
        foreach (var result in ConvertInlineNode(node, 0))
            yield return result;
    }

    private static IEnumerable<JObject> ConvertInlineNode(HtmlNode node, int inheritedFormat)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText) ?? string.Empty;
            if (text.Length > 0)
                yield return MakeTextNode(text, inheritedFormat);
            yield break;
        }

        if (node.NodeType != HtmlNodeType.Element) yield break;

        var childFormat = inheritedFormat;
        switch (node.Name.ToLowerInvariant())
        {
            case "strong": case "b": childFormat |= Bold; break;
            case "em": case "i": childFormat |= Italic; break;
            case "u": childFormat |= Underline; break;
            case "s": case "del": case "strike": childFormat |= Strikethrough; break;
            case "code": childFormat |= Code; break;
            case "br":
                yield return MakeLineBreak();
                yield break;
            case "a":
                yield return MakeLink(node.GetAttributeValue("href", "#"), ConvertInlineNodes(node.ChildNodes));
                yield break;
        }

        foreach (var child in node.ChildNodes)
        foreach (var result in ConvertInlineNode(child, childFormat))
            yield return result;
    }

    #endregion

    #region Node builders

    private static JObject MakeTextNode(string text, int format) => new()
    {
        ["type"] = "text", ["text"] = text, ["format"] = format,
        ["mode"] = "normal", ["style"] = "", ["detail"] = 0, ["version"] = 1
    };

    private static JObject MakeLineBreak() => new() { ["type"] = "linebreak", ["version"] = 1 };

    private static JObject MakeParagraph(IEnumerable<JObject> children) => new()
    {
        ["type"] = "paragraph", ["format"] = "", ["indent"] = 0,
        ["version"] = 1, ["direction"] = "ltr", ["children"] = new JArray(children)
    };

    private static JObject MakeHeading(string tag, IEnumerable<JObject> children) => new()
    {
        ["type"] = "heading", ["tag"] = tag, ["format"] = "", ["indent"] = 0,
        ["version"] = 1, ["direction"] = "ltr", ["children"] = new JArray(children)
    };

    private static JObject MakeQuote(IEnumerable<JObject> children) => new()
    {
        ["type"] = "quote", ["format"] = "", ["indent"] = 0,
        ["version"] = 1, ["direction"] = "ltr", ["children"] = new JArray(children)
    };

    private static JObject MakeList(string listType, IEnumerable<JObject> items) => new()
    {
        ["type"] = "list", ["listType"] = listType, ["start"] = 1,
        ["tag"] = listType == "number" ? "ol" : "ul",
        ["format"] = "", ["indent"] = 0, ["version"] = 1,
        ["direction"] = "ltr", ["children"] = new JArray(items)
    };

    private static JObject MakeListItem(IEnumerable<JObject> children) => new()
    {
        ["type"] = "listitem", ["value"] = 1, ["checked"] = false,
        ["format"] = "", ["indent"] = 0, ["version"] = 1,
        ["direction"] = "ltr", ["children"] = new JArray(children)
    };

    private static JObject MakeLink(string url, IEnumerable<JObject> children) => new()
    {
        ["type"] = "link", ["format"] = "", ["indent"] = 0, ["version"] = 1,
        ["direction"] = "ltr",
        ["fields"] = new JObject { ["url"] = url, ["newTab"] = false, ["linkType"] = "custom" },
        ["children"] = new JArray(children)
    };

    #endregion
}
