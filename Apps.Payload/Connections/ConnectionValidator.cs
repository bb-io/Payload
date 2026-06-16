using Apps.Payload.Api;
using Apps.Payload.Constants;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Connections;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Apps.Payload.Connections;

public class ConnectionValidator(InvocationContext invocationContext)
    : BaseInvocable(invocationContext), IConnectionValidator
{
    public async ValueTask<ConnectionValidationResponse> ValidateConnection(
        IEnumerable<AuthenticationCredentialsProvider> authProviders,
        CancellationToken cancellationToken)
    {
        try
        {
            var slug = authProviders.Get(CredNames.AuthCollectionSlug).Value;
            var client = new PayloadRestClient(authProviders);
            var request = new RestRequest($"/api/{slug}/me");
            var response = await client.ExecuteAsync(request, cancellationToken);

            var statusCode = (int)response.StatusCode;
            if (statusCode is 401 or 403 or 404)
                return Invalid($"Authentication failed ({statusCode}).");

            if (response.ResponseStatus != ResponseStatus.Completed)
                return Invalid(response.ErrorException?.Message ?? "Could not reach the server.");

            var body = JsonConvert.DeserializeObject<JObject>(response.Content ?? "{}");
            if (body?["user"] == null || body["user"]!.Type == JTokenType.Null)
                return Invalid("User is null in /me response. Verify the API key and collection slug.");

            return new ConnectionValidationResponse { IsValid = true };
        }
        catch (Exception ex)
        {
            return Invalid(ex.Message);
        }
    }

    private static ConnectionValidationResponse Invalid(string message) =>
        new() { IsValid = false, Message = message };
}
