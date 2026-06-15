using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.Payload.Models.Requests;

public class UploadContentRequest
{
    [Display("File", Description = "HTML or XLF file containing the translated content")]
    public required FileReference File { get; set; }

    [Display("Target locale", Description = "Locale of the translated content to write back (e.g. es, fr)")]
    public required string TargetLocale { get; set; }
}
