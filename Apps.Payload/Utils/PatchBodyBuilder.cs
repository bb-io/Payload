using Blackbird.Applications.Sdk.Common.Exceptions;
using Newtonsoft.Json.Linq;

namespace Apps.Payload.Utils;

public static class PatchBodyBuilder
{
    private const char PathSeparator = '.';

    public static JObject Build(Dictionary<string, object> fields, JObject? currentDocument, JObject? sourceDocument)
    {
        var body = new JObject();
        foreach (var (path, value) in fields)
            SetValueAtPath(body, currentDocument, path, ToToken(value, GetValueAtPath(sourceDocument, path)));

        return body;
    }

    public static bool ContainsArrayIndex(string path) =>
        path.Split(PathSeparator).Any(IsArrayIndex);

    public static bool ContainsRichText(Dictionary<string, object> fields) =>
        fields.Values.Any(value => value is RichTextTranslation);

    private static JToken? GetValueAtPath(JObject? document, string path) =>
        path.Split(PathSeparator).Aggregate<string, JToken?>(document, GetChild);

    private static void SetValueAtPath(JObject body, JObject? currentDocument, string path, JToken value)
    {
        var segments = path.Split(PathSeparator);
        JToken bodyNode = body;
        JToken? documentNode = currentDocument;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            documentNode = GetChild(documentNode, segments[i]);
            var bodyChild = GetChild(bodyNode, segments[i]);
            if (bodyChild == null)
            {
                bodyChild = documentNode is JArray currentRows ? currentRows.DeepClone() : new JObject();
                SetChild(bodyNode, segments[i], bodyChild, path);
            }

            if (IsArrayIndex(segments[i + 1]) && bodyChild is not JArray)
                throw StructureChanged(path);

            bodyNode = bodyChild;
        }

        SetChild(bodyNode, segments[^1], value, path);
    }

    private static JToken? GetChild(JToken? node, string segment) => node switch
    {
        JObject obj => obj[segment],
        JArray array when int.TryParse(segment, out var index) && index < array.Count => array[index],
        _ => null
    };

    private static void SetChild(JToken node, string segment, JToken value, string path)
    {
        switch (node)
        {
            case JObject obj:
                obj[segment] = value;
                break;
            case JArray array when int.TryParse(segment, out var index) && index < array.Count:
                array[index] = value;
                break;
            default:
                throw StructureChanged(path);
        }
    }

    private static PluginApplicationException StructureChanged(string path) =>
        new($"Field '{path}' no longer matches the structure of the content. Download the content again and retranslate it.");

    private static bool IsArrayIndex(string segment) =>
        segment.Length > 0 && segment.All(char.IsDigit);

    private static JToken ToToken(object value, JToken? sourceValue) => value switch
    {
        RichTextTranslation richText when sourceValue is JObject sourceLexical =>
            LexicalHtmlConverter.MergeTranslation(sourceLexical, richText.Html),
        RichTextTranslation richText => LexicalHtmlConverter.FromHtml(richText.Html),
        JToken token => token,
        string str => new JValue(str),
        _ => JToken.FromObject(value)
    };
}
