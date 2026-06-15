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
            if (excludeSet.Contains(property.Name) || property.Value is not JObject valueObj)
                continue;

            if (IsLocalizableField(valueObj))
                AppendLocalizableField(sb, property.Name, valueObj, sourceLocale, indent: "  ");
            else if (includeReferenceContent && IsReference(valueObj))
                AppendReferenceBlock(sb, property.Name, valueObj, sourceLocale);
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

    private static void AppendLocalizableField(StringBuilder sb, string fieldName, JObject localeValues, string sourceLocale, string indent)
    {
        if (!localeValues.ContainsKey(sourceLocale)) return;
        sb.AppendLine($"{indent}<div {HtmlConstants.JsonPath}=\"{HtmlEntity.Entitize(fieldName)}\">");
        AppendLocaleValue(sb, sourceLocale, localeValues[sourceLocale]!, indent + "  ");
        sb.AppendLine($"{indent}</div>");
    }

    private static void AppendLocaleValue(StringBuilder sb, string locale, JToken value, string indent)
    {
        var fieldType = value is JObject ? HtmlConstants.FieldTypeLexical : HtmlConstants.FieldTypeText;
        var content = value is JObject jObj
            ? LexicalHtmlConverter.ToHtml(jObj)
            : HtmlEntity.Entitize(value.ToString());
        sb.AppendLine($"{indent}<div {HtmlConstants.Locale}=\"{locale}\" {HtmlConstants.FieldType}=\"{fieldType}\">{content}</div>");
    }

    private static void AppendReferenceBlock(StringBuilder sb, string fieldName, JObject reference, string sourceLocale)
    {
        var refId = reference["id"]?.ToString() ?? string.Empty;
        sb.AppendLine($"  <article {HtmlConstants.ReferenceCollection}=\"{HtmlEntity.Entitize(fieldName)}\" {HtmlConstants.ReferenceId}=\"{HtmlEntity.Entitize(refId)}\">");

        foreach (var prop in reference.Properties())
        {
            if (prop.Name == "id" || prop.Value is not JObject sub) continue;
            if (IsLocalizableField(sub))
                AppendLocalizableField(sb, prop.Name, sub, sourceLocale, indent: "    ");
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

    private static bool IsReference(JObject obj) =>
        obj.ContainsKey("id") &&
        obj.Properties().Any(p => p.Name != "id" && p.Value is JObject sub && IsLocalizableField(sub));

    private static bool IsLocaleCode(string key)
    {
        // BCP 47: 2-3 lowercase language letters, optionally followed by subtags: en, es, en-US, zh-TW, pt-BR
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
