using Blackbird.Applications.SDK.Blueprints.Interfaces.CMS;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.Payload.Models.Responses;

public class DownloadContentOutput : IDownloadContentOutput
{
    public FileReference Content { get; set; } = null!;

    [Display("Main content ID")]
    public string RootContentId { get; set; } = string.Empty;
}
