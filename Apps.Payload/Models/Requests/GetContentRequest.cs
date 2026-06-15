using Blackbird.Applications.Sdk.Common;

namespace Apps.Payload.Models.Requests;

public class GetContentRequest
{
    [Display("Content type", Description = "The collection slug (e.g. posts, articles)")]
    public required string ContentType { get; set; }

    [Display("Content ID", Description = "The ID of the content item to retrieve")]
    public required string ContentId { get; set; }

    [Display("Locale", Description = "Locale to retrieve the content in")]
    public string? Locale { get; set; }
}
