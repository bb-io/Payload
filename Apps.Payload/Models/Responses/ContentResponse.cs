using Blackbird.Applications.Sdk.Common;
using Newtonsoft.Json;

namespace Apps.Payload.Models.Responses;

public class ContentResponse
{
    [Display("Content ID")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [Display("Status")]
    [JsonProperty("status")]
    public string? Status { get; set; }

    [Display("Updated at")]
    [JsonProperty("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [Display("Created at")]
    [JsonProperty("createdAt")]
    public DateTime? CreatedAt { get; set; }
}
