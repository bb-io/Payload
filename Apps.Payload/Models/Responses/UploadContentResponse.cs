using Blackbird.Applications.Sdk.Common;

namespace Apps.Payload.Models.Responses;

public class UploadContentResponse
{
    [Display("Content type")]
    public string ContentType { get; set; } = string.Empty;

    [Display("Content ID")]
    public string ContentId { get; set; } = string.Empty;

    [Display("Error messages")]
    public IEnumerable<string>? ErrorMessages { get; set; }
}
