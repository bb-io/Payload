using Blackbird.Applications.Sdk.Common;
using Newtonsoft.Json;

namespace Apps.Payload.Models.Responses;

public class ContentEventResponse
{
    [Display("Event")]
    [JsonProperty("event")]
    public string? Event { get; set; }

    [Display("Content type")]
    [JsonProperty("contentType")]
    public string? ContentType { get; set; }

    [Display("Content ID")]
    [JsonProperty("contentId")]
    public string? ContentId { get; set; }

    [Display("Timestamp")]
    [JsonProperty("timestamp")]
    public DateTime? Timestamp { get; set; }
}
