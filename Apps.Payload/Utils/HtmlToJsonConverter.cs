using Apps.Payload.Constants;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Apps.Payload.Utils;

public record ParsedContent(
    string Ucid,
    Dictionary<string, object> MainFields,
    List<ReferenceUpdate> References
);

public record ReferenceUpdate(
    string Collection,
    string Id,
    Dictionary<string, object> Fields
);

public static class HtmlToJsonConverter
{
    public static ParsedContent Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var ucid = ExtractMeta(doc, HtmlConstants.MetaUcid);
        var locale = ExtractMeta(doc, HtmlConstants.MetaLocale);
        var mainFields = ExtractFields(doc.DocumentNode, locale, insideReference: false);
        var references = ExtractReferences(doc.DocumentNode, locale);

        return new ParsedContent(ucid, mainFields, references);
    }

    private static string ExtractMeta(HtmlDocument doc, string metaName) =>
        doc.DocumentNode
            .Descendants("meta")
            .FirstOrDefault(n => n.GetAttributeValue("name", "") == metaName)
            ?.GetAttributeValue("content", "") ?? string.Empty;

    private static Dictionary<string, object> ExtractFields(
        HtmlNode root,
        string targetLocale,
        bool insideReference)
    {
        var fields = new Dictionary<string, object>();

        var fieldNodes = root.Descendants("div")
            .Where(n => n.Attributes.Contains(HtmlConstants.JsonPath));

        foreach (var fieldNode in fieldNodes)
        {
            var isInReference = IsInsideReference(fieldNode);
            if (isInReference != insideReference) continue;

            var fieldName = fieldNode.GetAttributeValue(HtmlConstants.JsonPath, string.Empty);
            if (string.IsNullOrEmpty(fieldName)) continue;

            var localeNode = fieldNode.ChildNodes
                .FirstOrDefault(n =>
                    n.Name == "div" &&
                    n.GetAttributeValue(HtmlConstants.Locale, "") == targetLocale);

            if (localeNode == null) continue;

            var fieldType = localeNode.GetAttributeValue(HtmlConstants.FieldType, HtmlConstants.FieldTypeText);
            object value = fieldType == HtmlConstants.FieldTypeLexical
                ? LexicalHtmlConverter.FromHtml(localeNode.InnerHtml)
                : (HtmlEntity.DeEntitize(localeNode.InnerText) ?? string.Empty);

            fields[fieldName] = value;
        }

        return fields;
    }

    private static List<ReferenceUpdate> ExtractReferences(HtmlNode root, string targetLocale)
    {
        var references = new List<ReferenceUpdate>();

        var articleNodes = root.Descendants("article")
            .Where(n => n.Attributes.Contains(HtmlConstants.ReferenceId));

        foreach (var article in articleNodes)
        {
            var collection = article.GetAttributeValue(HtmlConstants.ReferenceCollection, string.Empty);
            var id = article.GetAttributeValue(HtmlConstants.ReferenceId, string.Empty);
            if (string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(id)) continue;

            var fields = ExtractFields(article, targetLocale, insideReference: true);
            if (fields.Count > 0)
                references.Add(new ReferenceUpdate(collection, id, fields));
        }

        return references;
    }

    private static bool IsInsideReference(HtmlNode node)
    {
        var parent = node.ParentNode;
        while (parent != null)
        {
            if (parent.Name == "article" && parent.Attributes.Contains(HtmlConstants.ReferenceId))
                return true;
            parent = parent.ParentNode;
        }
        return false;
    }
}
