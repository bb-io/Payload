using Blackbird.Applications.SDK.Blueprints.Interfaces.CMS;
using Blackbird.Applications.Sdk.Common;

namespace Apps.Payload.Models.Requests;

public class DownloadContentRequest : IDownloadContentInput
{
    [Display("Content type", Description = "The collection slug (e.g. posts, articles)")]
    public required string ContentType { get; set; }

    [Display("Content ID", Description = "The ID of the content item to download")]
    public required string ContentId { get; set; }

    [Display("Fields to exclude", Description = "Field names to omit from the HTML output")]
    public IEnumerable<string>? FieldsToExclude { get; set; }

    [Display("Include reference content", Description = "When true, localizable fields of referenced documents are included in the HTML")]
    public bool? IncludeReferenceContent { get; set; }

    [Display("Locale", Description = "The locale of the content to download (e.g. en, es, en-US). One file contains one language.")]
    public required string Locale { get; set; }
}
