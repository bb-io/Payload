using Blackbird.Applications.SDK.Blueprints.Interfaces.CMS;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.Payload.Models.Requests;

public class UploadContentRequest : IUploadContentInput
{
    [Display("File", Description = "HTML or XLF file containing the translated content")]
    public required FileReference Content { get; set; }

    [Display("Target locale", Description = "Locale of the translated content to write back (e.g. es, fr)")]
    public required string Locale { get; set; }

    [Display("Content ID", Description = "ID of the content to update. If not provided, a new content will be created.")]
    public string? ContentId { get; set; }
}
