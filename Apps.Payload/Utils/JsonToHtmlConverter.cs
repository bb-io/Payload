using System.Text;
using Apps.Payload.Constants;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Apps.Payload.Utils;

public static class JsonToHtmlConverter
{
    public static string Convert(
        JObject content,
        string contentType,
        string contentId,
        string sourceLocale,
        IEnumerable<string>? fieldsToExclude = null,
        bool includeReferenceContent = false)
    {
        var sb = new StringBuilder();
        var excludeSet = (fieldsToExclude ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);

        AppendDocumentHead(sb, contentType, contentId, sourceLocale);
        sb.AppendLine("<body>");

        foreach (var property in content.Properties())
        {
            if (excludeSet.Contains(property.Name))
                continue;

            if (property.Value is JObject valueObj && IsLocalizableField(valueObj))
                AppendLocalizableField(sb, property.Name, valueObj, sourceLocale, contentType, contentId, indent: "  ");
            else if (property.Value is JObject referenceObj && includeReferenceContent && IsReference(referenceObj))
                AppendReferenceBlock(sb, property.Name, referenceObj, sourceLocale);
            else
                AppendNestedFields(sb, property.Value, property.Name, sourceLocale, contentType, contentId, excludeSet);
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static void AppendDocumentHead(StringBuilder sb, string contentType, string contentId, string sourceLocale)
    {
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine($"<html lang=\"{sourceLocale}\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        AppendMeta(sb, HtmlConstants.MetaUcid, $"{contentType}:{contentId}");
        AppendMeta(sb, HtmlConstants.MetaLocale, sourceLocale);
        AppendMeta(sb, HtmlConstants.MetaSystemName, "Payload CMS");
        AppendMeta(sb, HtmlConstants.MetaSystemRef, "https://payloadcms.com");
        sb.AppendLine("</head>");
    }

    private static void AppendNestedFields(StringBuilder sb, JToken container, string path, string sourceLocale, string contentType, string contentId, HashSet<string> excludeSet)
    {
        if (container is JArray rows)
        {
            for (var i = 0; i < rows.Count; i++)
                if (rows[i] is JObject row && !IsPopulatedDocument(row))
                    AppendNestedObjectFields(sb, row, $"{path}.{i}", sourceLocale, contentType, contentId, excludeSet);
            return;
        }

        if (container is JObject group && !group.ContainsKey("id") && !LexicalHtmlConverter.IsLexical(group))
            AppendNestedObjectFields(sb, group, path, sourceLocale, contentType, contentId, excludeSet);
    }

    private static void AppendNestedObjectFields(StringBuilder sb, JObject container, string path, string sourceLocale, string contentType, string contentId, HashSet<string> excludeSet)
    {
        foreach (var property in container.Properties())
        {
            var fieldPath = $"{path}.{property.Name}";
            if (excludeSet.Contains(property.Name) || excludeSet.Contains(fieldPath))
                continue;

            if (property.Value is JObject valueObj && IsLocalizableField(valueObj))
                AppendLocalizableField(sb, fieldPath, valueObj, sourceLocale, contentType, contentId, indent: "  ");
            else
                AppendNestedFields(sb, property.Value, fieldPath, sourceLocale, contentType, contentId, excludeSet);
        }
    }

    private static void AppendLocalizableField(StringBuilder sb, string fieldName, JObject localeValues, string sourceLocale, string contentType, string contentId, string indent)
    {
        if (!localeValues.TryGetValue(sourceLocale, out var sourceValue) || !IsTranslatableValue(sourceValue)) return;
        var key = $"{contentType}-{contentId}-{fieldName}";
        sb.AppendLine($"{indent}<div {HtmlConstants.JsonPath}=\"{HtmlEntity.Entitize(fieldName)}\">");
        AppendLocaleValue(sb, sourceLocale, sourceValue, key, indent + "  ");
        sb.AppendLine($"{indent}</div>");
    }

    private static void AppendLocaleValue(StringBuilder sb, string locale, JToken value, string blackbirdKey, string indent)
    {
        var fieldType = value is JObject ? HtmlConstants.FieldTypeLexical : HtmlConstants.FieldTypeText;
        var content = value is JObject jObj
            ? LexicalHtmlConverter.ToHtml(jObj)
            : HtmlEntity.Entitize(value.ToString());
        sb.AppendLine($"{indent}<div {HtmlConstants.Locale}=\"{locale}\" {HtmlConstants.FieldType}=\"{fieldType}\" {HtmlConstants.BlackbirdKey}=\"{HtmlEntity.Entitize(blackbirdKey)}\">{content}</div>");
    }

    private static void AppendReferenceBlock(StringBuilder sb, string fieldName, JObject reference, string sourceLocale)
    {
        var refId = reference["id"]?.ToString() ?? string.Empty;
        sb.AppendLine($"  <article {HtmlConstants.ReferenceCollection}=\"{HtmlEntity.Entitize(fieldName)}\" {HtmlConstants.ReferenceId}=\"{HtmlEntity.Entitize(refId)}\">");

        foreach (var prop in reference.Properties())
        {
            if (prop.Name == "id" || prop.Value is not JObject sub) continue;
            if (IsLocalizableField(sub))
                AppendLocalizableField(sb, prop.Name, sub, sourceLocale, fieldName, refId, indent: "    ");
        }

        sb.AppendLine("  </article>");
    }

    private static void AppendMeta(StringBuilder sb, string name, string content) =>
        sb.AppendLine($"  <meta name=\"{name}\" content=\"{HtmlEntity.Entitize(content)}\">");

    internal static bool IsLocalizableField(JObject obj)
    {
        var props = obj.Properties().ToList();
        return props.Count > 0 && props.All(p => IsLocaleCode(p.Name));
    }

    private static bool IsTranslatableValue(JToken value) =>
        value is JValue || value is JObject obj && LexicalHtmlConverter.IsLexical(obj);

    private static bool IsPopulatedDocument(JObject obj) =>
        obj.ContainsKey("id") && (obj.ContainsKey("createdAt") || obj.ContainsKey("updatedAt"));

    private static bool IsReference(JObject obj) =>
        obj.ContainsKey("id") &&
        obj.Properties().Any(p => p.Name != "id" && p.Value is JObject sub && IsLocalizableField(sub));

    private static bool IsLocaleCode(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        var parts = key.Split('-');
        if (parts[0].Length is < 2 or > 3 || !parts[0].All(char.IsLower))
            return false;
        for (var i = 1; i < parts.Length; i++)
            if (parts[i].Length is < 2 or > 4 || !parts[i].All(char.IsLetterOrDigit))
                return false;
        return true;
    }
}
