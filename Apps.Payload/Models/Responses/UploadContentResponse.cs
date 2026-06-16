using Blackbird.Applications.SDK.Blueprints.Interfaces.CMS;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.Payload.Models.Responses;

public class UploadContentResponse : IDownloadContentOutput
{
    [Display("Content type")]
    public string ContentType { get; set; } = string.Empty;

    [Display("Content ID")]
    public string ContentId { get; set; } = string.Empty;

    [Display("Error messages")]
    public IEnumerable<string>? ErrorMessages { get; set; }

    public FileReference Content { get; set; } = null!;
}
