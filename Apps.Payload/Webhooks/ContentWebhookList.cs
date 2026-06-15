using Apps.Payload.Invocables;
using Apps.Payload.Models.Responses;
using Blackbird.Applications.SDK.Blueprints;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Webhooks;
using Newtonsoft.Json;

namespace Apps.Payload.Webhooks;

[WebhookList]
public class ContentWebhookList(InvocationContext invocationContext) : PayloadInvocable(invocationContext)
{
    
    [BlueprintEventDefinition(BlueprintEvent.ContentCreatedOrUpdated)]
    [Webhook("On content triggered",
        Description = "Fires when Payload CMS sends a webhook event. Configure the callback URL shown here in your Payload instance's hook settings.")]
    public Task<WebhookResponse<ContentEventResponse>> OnContentTriggered(WebhookRequest request)
    {
        var body = request.Body?.ToString();
        var dto = string.IsNullOrWhiteSpace(body)
            ? new ContentEventResponse()
            : JsonConvert.DeserializeObject<ContentEventResponse>(body) ?? new ContentEventResponse();

        return Task.FromResult(new WebhookResponse<ContentEventResponse>
        {
            HttpResponseMessage = null,
            ReceivedWebhookRequestType = WebhookRequestType.Default,
            Result = dto
        });
    }
}
