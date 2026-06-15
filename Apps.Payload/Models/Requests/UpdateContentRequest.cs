using Blackbird.Applications.Sdk.Common;

namespace Apps.Payload.Models.Requests;

public class UpdateContentRequest
{
    [Display("Content type", Description = "The collection slug (e.g. posts, articles)")]
    public required string ContentType { get; set; }

    [Display("Content ID", Description = "The ID of the content item to update")]
    public required string ContentId { get; set; }

    [Display("Locale", Description = "Target locale to write the field values into")]
    public string? Locale { get; set; }

    [Display("Field keys", Description = "Names of the fields to update")]
    public required IEnumerable<string> FieldKeys { get; set; }

    [Display("Field values", Description = "Values corresponding to each field key (order must match)")]
    public required IEnumerable<string> FieldValues { get; set; }
}
