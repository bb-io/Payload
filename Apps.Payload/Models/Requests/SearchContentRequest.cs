using Blackbird.Applications.Sdk.Common;

namespace Apps.Payload.Models.Requests;

public class SearchContentRequest
{
    [Display("Content type", Description = "The collection slug to search (e.g. posts, articles)")]
    public required string ContentType { get; set; }

    [Display("Locale", Description = "Filter results to a specific locale")]
    public string? Locale { get; set; }
}
