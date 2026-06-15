using Apps.Payload.Constants;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Connections;

namespace Apps.Payload.Connections;

public class ConnectionDefinition : IConnectionDefinition
{
    public IEnumerable<ConnectionPropertyGroup> ConnectionPropertyGroups =>
    [
        new()
        {
            Name = "API key",
            AuthenticationType = ConnectionAuthenticationType.Undefined,
            ConnectionProperties =
            [
                new(CredNames.BaseUrl)
                {
                    DisplayName = "Base URL",
                    Description = "The base URL of your Payload CMS instance (e.g. http://localhost:3000)"
                },
                new(CredNames.ApiKey)
                {
                    DisplayName = "API key",
                    Sensitive = true,
                    Description = "Full authorization value generated in Payload admin UI (format: {collection} API-Key {key}, e.g. users API-Key abc123)"
                },
                new(CredNames.AuthCollectionSlug)
                {
                    DisplayName = "Auth collection slug (e.g. users)",
                    Description = "Slug of the Payload collection whose API key is used for authentication"
                }
            ]
        }
    ];

    public IEnumerable<AuthenticationCredentialsProvider> CreateAuthorizationCredentialsProviders(Dictionary<string, string> values)
    {
        return new List<AuthenticationCredentialsProvider>
        {
            new(CredNames.BaseUrl, values[CredNames.BaseUrl]),
            new(CredNames.ApiKey, $"{values[CredNames.AuthCollectionSlug]} API-Key {values[CredNames.ApiKey]}"),
            new(CredNames.AuthCollectionSlug, values[CredNames.AuthCollectionSlug])
        };
    }
}
