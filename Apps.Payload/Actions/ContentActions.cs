using System.Text;
using Apps.Payload.Invocables;
using Apps.Payload.Models.Requests;
using Apps.Payload.Models.Responses;
using Apps.Payload.Utils;
using Blackbird.Applications.SDK.Blueprints;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Apps.Payload.Actions;

[ActionList("Content")]
public class ContentActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
    : PayloadInvocable(invocationContext)
{
    [Action("Search content", Description = "Returns all items from a content collection, automatically fetching all pages.")]
    public async Task<SearchContentResponse> SearchContent([ActionParameter] SearchContentRequest request)
    {
        var queryParams = new Dictionary<string, string?>();
        if (!string.IsNullOrEmpty(request.Locale))
            queryParams["locale"] = request.Locale;

        var items = await Client.PaginateAsync<ContentResponse>($"{request.ContentType}", queryParams);
        return new SearchContentResponse { Items = items };
    }

    [Action("Get content", Description = "Returns a single content item by type and ID.")]
    public async Task<ContentResponse> GetContent([ActionParameter] GetContentRequest request)
    {
        var req = new RestRequest($"{request.ContentType}/{request.ContentId}");
        req.AddQueryParameter("depth", "1");

        if (!string.IsNullOrEmpty(request.Locale))
            req.AddQueryParameter("locale", request.Locale);

        return await Client.ExecuteWithErrorHandling<ContentResponse>(req);
    }

    [BlueprintActionDefinition(BlueprintAction.DownloadContent)]
    [Action("Download content",
        Description = "Downloads all localizable fields as an HTML file, ready for translation.")]
    public async Task<DownloadContentOutput> DownloadContent([ActionParameter] DownloadContentRequest request)
    {
        var req = new RestRequest($"{request.ContentType}/{request.ContentId}");
        req.AddQueryParameter("locale", "all");
        req.AddQueryParameter("depth", request.IncludeReferenceContent == true ? "1" : "0");

        var content = await Client.ExecuteWithErrorHandling<JObject>(req);

        var html = JsonToHtmlConverter.Convert(
            content,
            request.ContentType,
            request.ContentId,
            request.Locale,
            request.FieldsToExclude,
            request.IncludeReferenceContent == true);

        var bytes = Encoding.UTF8.GetBytes(html);
        var fileName = $"{request.ContentType}_{request.ContentId}.html";
        return new()
        {
            Content = await fileManagementClient.UploadAsync(new MemoryStream(bytes), "text/html", fileName),
            RootContentId = request.ContentId
        };
    }

    [Action("Update content",
        Description = "Updates specified fields of a content item for a given locale via PATCH.")]
    public async Task<ContentResponse> UpdateContent([ActionParameter] UpdateContentRequest request)
    {
        var keys = request.FieldKeys.ToList();
        var values = request.FieldValues.ToList();
        var body = new JObject();
        for (var i = 0; i < keys.Count; i++)
            body[keys[i]] = i < values.Count ? values[i] : null;

        var req = new RestRequest($"{request.ContentType}/{request.ContentId}", Method.Patch);
        if (!string.IsNullOrEmpty(request.Locale))
            req.AddQueryParameter("locale", request.Locale);
        req.AddJsonBody(body);

        var result = await Client.ExecuteWithErrorHandling<JObject>(req);
        return result["doc"]?.ToObject<ContentResponse>() ?? result.ToObject<ContentResponse>()!;
    }

    [Action("Publish content",
        Description = "Publishes the latest draft of a content item by setting its status to published.")]
    public async Task<ContentResponse> PublishContent([ActionParameter] PublishContentRequest request)
    {
        var req = new RestRequest($"{request.ContentType}/{request.ContentId}", Method.Patch)
            .AddJsonBody(new JObject { ["_status"] = "published" }.ToString(Formatting.None));

        if (!string.IsNullOrEmpty(request.Locale))
            req.AddQueryParameter("locale", request.Locale);

        var result = await Client.ExecuteWithErrorHandling<JObject>(req);
        return result["doc"]?.ToObject<ContentResponse>() ?? result.ToObject<ContentResponse>()!;
    }

    [BlueprintActionDefinition(BlueprintAction.UploadContent)]
    [Action("Upload content",
        Description = "Updates a content item from a translated HTML or XLF file.")]
    public async Task<UploadContentResponse> UploadContent([ActionParameter] UploadContentRequest request)
    {
        var fileStream = await fileManagementClient.DownloadAsync(request.Content);
        var html = TranslatedFileReader.ReadHtml(await fileStream.GetByteData(), request.Content.Name);

        var parsed = HtmlToJsonConverter.Parse(html);
        var ucidParts = parsed.Ucid.Split(':', 2);
        var contentType = ucidParts[0];
        var contentId = ucidParts.Length > 1 ? ucidParts[1] : string.Empty;

        var saveAsDraft = request.SaveAsDraft == true;
        await PatchContent(contentType, contentId, parsed.SourceLocale, request.Locale, parsed.MainFields, saveAsDraft);

        var errors = new List<string>();
        foreach (var reference in parsed.References)
        {
            try
            {
                await PatchContent(reference.Collection, reference.Id, parsed.SourceLocale, request.Locale, reference.Fields, saveAsDraft);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to update {reference.Collection}/{reference.Id}: {ex.Message}");
            }
        }

        var downloadResponse = await DownloadContent(new DownloadContentRequest
        {
            ContentType = contentType,
            ContentId = contentId,
            Locale = request.Locale,
            IncludeReferenceContent = true
        });
        
        return new UploadContentResponse
        {
            ContentType = contentType,
            ContentId = contentId,
            ErrorMessages = errors.Count > 0 ? errors : null,
            Content = downloadResponse.Content
        };
    }

    private async Task PatchContent(string contentType, string contentId, string sourceLocale, string targetLocale, Dictionary<string, object> fields, bool saveAsDraft)
    {
        var currentDocument = fields.Keys.Any(PatchBodyBuilder.ContainsArrayIndex)
            ? await GetLocaleDocument(contentType, contentId, targetLocale, saveAsDraft)
            : null;
        var sourceDocument = PatchBodyBuilder.ContainsRichText(fields)
            ? await GetLocaleDocument(contentType, contentId, sourceLocale, latestDraft: false)
            : null;

        var body = PatchBodyBuilder.Build(fields, currentDocument, sourceDocument);
        var json = JsonConvert.SerializeObject(body, Formatting.Indented);
        var req = new RestRequest($"{contentType}/{contentId}", Method.Patch)
            .AddQueryParameter("locale", targetLocale)
            .AddJsonBody(json);

        if (saveAsDraft)
            req.AddQueryParameter("draft", "true");

        await Client.ExecuteWithErrorHandling(req);
    }

    private async Task<JObject> GetLocaleDocument(string contentType, string contentId, string locale, bool latestDraft)
    {
        var req = new RestRequest($"{contentType}/{contentId}")
            .AddQueryParameter("locale", locale)
            .AddQueryParameter("fallback-locale", "none")
            .AddQueryParameter("depth", "0");

        if (latestDraft)
            req.AddQueryParameter("draft", "true");

        return await Client.ExecuteWithErrorHandling<JObject>(req);
    }
}
