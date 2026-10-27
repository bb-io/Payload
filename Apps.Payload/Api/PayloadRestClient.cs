using Apps.Payload.Constants;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Blackbird.Applications.Sdk.Utils.Extensions.String;
using Blackbird.Applications.Sdk.Utils.RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Apps.Payload.Api;

public class PayloadRestClient : BlackBirdRestClient
{
    public PayloadRestClient(IEnumerable<AuthenticationCredentialsProvider> creds)
        : base(new RestClientOptions
        {
            BaseUrl = BuildBaseUrl(creds),
            Timeout = TimeSpan.FromSeconds(60)
        })
    {
        this.AddDefaultHeader("Authorization", creds.Get(CredNames.ApiKey).Value);
    }

    public async Task<IReadOnlyList<T>> PaginateAsync<T>(
        string resource,
        IDictionary<string, string?>? queryParameters = null)
    {
        int page = 1;
        int pageSize = 100;
        var results = new List<T>();
        PagedResponse<T>? response;

        do
        {
            var request = new RestRequest(resource);

            if (queryParameters != null)
                foreach (var (key, value) in queryParameters)
                    if (value != null)
                        request.AddQueryParameter(key, value);

            request.AddQueryParameter("page", page.ToString());
            request.AddQueryParameter("limit", pageSize.ToString());

            response = await ExecuteWithErrorHandling<PagedResponse<T>>(request);
            if (response?.Docs != null)
                results.AddRange(response.Docs);

            page++;
        } while (response?.HasNextPage == true);

        return results;
    }

    protected override Exception ConfigureErrorException(RestResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.Content))
            return new PluginApplicationException(
                $"Payload CMS returned {(int)response.StatusCode} {response.StatusDescription}.");

        try
        {
            var body = JsonConvert.DeserializeObject<JObject>(response.Content);
            var message = body?["errors"]?.FirstOrDefault()?["message"]?.ToString()
                          ?? body?["message"]?.ToString()
                          ?? response.Content;
            return new PluginApplicationException(message);
        }
        catch
        {
            return new PluginApplicationException(
                $"Payload CMS error ({(int)response.StatusCode}): {response.Content}");
        }
    }
    
    private static Uri BuildBaseUrl(IEnumerable<AuthenticationCredentialsProvider> creds)
    {
        string baseUrl = creds.Get(CredNames.BaseUrl).Value.TrimEnd('/');
        string apiPath = creds.FirstOrDefault(c => c.KeyName == CredNames.ApiPath)?.Value;

        if (string.IsNullOrWhiteSpace(apiPath))
            apiPath = "/api";

        return $"{baseUrl}/{apiPath.Trim('/')}".ToUri();
    }

    private class PagedResponse<T>
    {
        [JsonProperty("docs")]
        public List<T>? Docs { get; set; }

        [JsonProperty("hasNextPage")]
        public bool HasNextPage { get; set; }
    }
}
