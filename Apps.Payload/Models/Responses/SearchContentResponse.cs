using Blackbird.Applications.Sdk.Common;

namespace Apps.Payload.Models.Responses;

public class SearchContentResponse
{
    [Display("Content items")]
    public IEnumerable<ContentResponse> Items { get; set; } = [];
}
