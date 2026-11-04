using Blackbird.Applications.Sdk.Common;

namespace Apps.Payload.Models.Requests;

public class PublishContentRequest
{
    [Display("Content type", Description = "The collection slug (e.g. posts, articles)")]
    public required string ContentType { get; set; }

    [Display("Content ID", Description = "The ID of the content item to publish")]
    public required string ContentId { get; set; }

    [Display("Locale", Description = "Locale to publish (e.g. en, es, fr)")]
    public string? Locale { get; set; }
}
