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
            Name = ConnectionTypes.ApiKey,
            DisplayName = "API key",
            AuthenticationType = ConnectionAuthenticationType.Undefined,
            ConnectionProperties = CommonProperties
        },
        new()
        {
            Name = ConnectionTypes.ApiKeyCustomPath,
            DisplayName = "API key (custom API path)",
            AuthenticationType = ConnectionAuthenticationType.Undefined,
            ConnectionProperties =
            [
                ..CommonProperties,
                new(CredNames.ApiPath)
                {
                    DisplayName = "API path",
                    Description = "Custom REST API route from your Payload config (routes.api), e.g. /api/v1"
                }
            ]
        },
    ];

    public IEnumerable<AuthenticationCredentialsProvider> CreateAuthorizationCredentialsProviders(Dictionary<string, string> values)
    {
        string connectionType = values[nameof(ConnectionPropertyGroup)] switch
        {
            var ct when ConnectionTypes.SupportedConnectionTypes.Contains(ct) => ct,
            _ => throw new Exception($"Unknown connection type: {values[nameof(ConnectionPropertyGroup)]}")
        };
        
        return new List<AuthenticationCredentialsProvider>
        {
            new(CredNames.BaseUrl, values[CredNames.BaseUrl]),
            new(CredNames.ApiKey, $"{values[CredNames.AuthCollectionSlug]} API-Key {values[CredNames.ApiKey]}"),
            new(CredNames.AuthCollectionSlug, values[CredNames.AuthCollectionSlug]),
            new(CredNames.ApiPath, values.GetValueOrDefault(CredNames.ApiPath) ?? string.Empty),
            new(CredNames.ConnectionType, connectionType)
        };
    }
    
    private static List<ConnectionProperty> CommonProperties =>
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
    ];
}
