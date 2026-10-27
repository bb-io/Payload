namespace Apps.Payload.Constants;

public static class ConnectionTypes
{
    public const string ApiKey = "API key";
    public const string ApiKeyCustomPath = "ApiKeyCustomPath";

    public static readonly string[] SupportedConnectionTypes = [ApiKey, ApiKeyCustomPath];
}