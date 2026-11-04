using System.Text;
using Apps.Payload.Constants;
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

    private static readonly HashSet<string> ContainerTypes = ["root", "list", "table", "tablerow", "tablecell"];

    public static bool IsLexical(JObject value) =>
        value["root"] is JObject root && root["type"]?.ToString() == "root";

    #region ToHtml

    public static string ToHtml(JToken lexicalRoot)
    {
        var root = lexicalRoot["root"] ?? lexicalRoot;
        var sb = new StringBuilder();
        AppendChildren(root["children"], sb, string.Empty);
        return sb.ToString();
    }

    private static void AppendChildren(JToken? children, StringBuilder sb, string parentPath)
    {
        if (children == null) return;
        var index = 0;
        foreach (var node in children)
            AppendNode(node, sb, ChildPath(parentPath, index++));
    }

    private static string ChildPath(string parentPath, int index) =>
        parentPath.Length == 0 ? index.ToString() : $"{parentPath}.{index}";

    private static void AppendNode(JToken node, StringBuilder sb, string path)
    {
        var pathAttribute = $" {HtmlConstants.LexicalPath}=\"{path}\"";
        switch (node["type"]?.ToString())
        {
            case "paragraph":
                sb.Append($"<p{pathAttribute}>");
                AppendChildren(node["children"], sb, path);
                sb.Append("</p>");
                break;

            case "heading":
                var tag = node["tag"]?.ToString() ?? "h2";
                sb.Append($"<{tag}{pathAttribute}>");
                AppendChildren(node["children"], sb, path);
                sb.Append($"</{tag}>");
                break;

            case "quote":
                sb.Append($"<blockquote{pathAttribute}>");
                AppendChildren(node["children"], sb, path);
                sb.Append("</blockquote>");
                break;

            case "list":
                var listTag = node["listType"]?.ToString() == "number" ? "ol" : "ul";
                sb.Append($"<{listTag}{pathAttribute}>");
                AppendChildren(node["children"], sb, path);
                sb.Append($"</{listTag}>");
                break;

            case "listitem":
                sb.Append($"<li{pathAttribute}>");
                AppendChildren(node["children"], sb, path);
                sb.Append("</li>");
                break;

            case "link":
            case "autolink":
                var url = node["fields"]?["url"]?.ToString() ?? node["url"]?.ToString() ?? "#";
                sb.Append($"<a href=\"{HtmlEntity.Entitize(url)}\"{pathAttribute}>");
                AppendChildren(node["children"], sb, path);
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
                AppendChildren(node["children"], sb, path);
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

        switch (node.Name.ToLowerInvariant())
        {
            case "br":
                yield return MakeLineBreak();
                yield break;
            case "a":
                yield return MakeLink(node.GetAttributeValue("href", "#"), ConvertInlineNodes(node.ChildNodes));
                yield break;
        }

        var childFormat = inheritedFormat | FormatOf(node);
        foreach (var child in node.ChildNodes)
        foreach (var result in ConvertInlineNode(child, childFormat))
            yield return result;
    }

    private static int FormatOf(HtmlNode node) => node.Name.ToLowerInvariant() switch
    {
        "strong" or "b" => Bold,
        "em" or "i" => Italic,
        "u" => Underline,
        "s" or "del" or "strike" => Strikethrough,
        "code" => Code,
        _ => 0
    };

    #endregion

    #region MergeTranslation

    public static JObject MergeTranslation(JObject originalLexical, string translatedHtml)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(translatedHtml);

        var translatedElements = doc.DocumentNode.Descendants()
            .Where(n => n.Attributes.Contains(HtmlConstants.LexicalPath))
            .GroupBy(n => n.GetAttributeValue(HtmlConstants.LexicalPath, string.Empty))
            .ToDictionary(g => g.Key, g => g.First());

        if (translatedElements.Count == 0 || !IsLexical(originalLexical))
            return FromHtml(translatedHtml);

        var merged = (JObject)originalLexical.DeepClone();
        MergeContainer((JObject)merged["root"]!, string.Empty, translatedElements);
        return merged;
    }

    private static void MergeContainer(JObject container, string path, Dictionary<string, HtmlNode> translatedElements)
    {
        if (container["children"] is not JArray children) return;

        for (var i = 0; i < children.Count; i++)
        {
            if (children[i] is not JObject child) continue;

            var childPath = ChildPath(path, i);
            if (translatedElements.TryGetValue(childPath, out var element))
                MergeNode(child, childPath, element, translatedElements);
            else if (IsContainer(child))
                MergeContainer(child, childPath, translatedElements);
        }
    }

    private static void MergeNode(JObject node, string path, HtmlNode element, Dictionary<string, HtmlNode> translatedElements)
    {
        if (IsContainer(node))
        {
            MergeContainer(node, path, translatedElements);
            return;
        }

        var originalChildren = node["children"] as JArray ?? [];
        node["children"] = new JArray(RebuildInlineNodes(element.ChildNodes, 0, originalChildren, path, translatedElements).ToList());
    }

    private static IEnumerable<JObject> RebuildInlineNodes(HtmlNodeCollection nodes, int format, JArray originalChildren, string parentPath, Dictionary<string, HtmlNode> translatedElements)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                var text = HtmlEntity.DeEntitize(node.InnerText) ?? string.Empty;
                if (text.Length > 0)
                    yield return MakeTextNode(text, format);
                continue;
            }

            if (node.NodeType != HtmlNodeType.Element) continue;

            var original = FindOriginalChild(node, originalChildren, parentPath);
            if (original != null)
            {
                var mergedChild = (JObject)original.DeepClone();
                MergeNode(mergedChild, node.GetAttributeValue(HtmlConstants.LexicalPath, string.Empty), node, translatedElements);
                yield return mergedChild;
                continue;
            }

            switch (node.Name.ToLowerInvariant())
            {
                case "br":
                    yield return MakeLineBreak();
                    continue;
                case "a":
                    var linkChildren = RebuildInlineNodes(node.ChildNodes, format, [], parentPath, translatedElements).ToList();
                    yield return MakeLink(HtmlEntity.DeEntitize(node.GetAttributeValue("href", "#")), linkChildren);
                    continue;
            }

            foreach (var child in RebuildInlineNodes(node.ChildNodes, format | FormatOf(node), originalChildren, parentPath, translatedElements))
                yield return child;
        }
    }

    private static JObject? FindOriginalChild(HtmlNode element, JArray originalChildren, string parentPath)
    {
        var path = element.GetAttributeValue(HtmlConstants.LexicalPath, string.Empty);
        var childPrefix = $"{parentPath}.";
        if (!path.StartsWith(childPrefix) || !int.TryParse(path[childPrefix.Length..], out var index))
            return null;

        return index < originalChildren.Count ? originalChildren[index] as JObject : null;
    }

    private static bool IsContainer(JObject node) =>
        ContainerTypes.Contains(node["type"]?.ToString() ?? string.Empty);

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
