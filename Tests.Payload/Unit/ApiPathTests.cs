using Apps.Payload.Api;
using Apps.Payload.Constants;
using Blackbird.Applications.Sdk.Common.Authentication;
using RestSharp;

namespace Tests.Payload.Unit;

[TestClass]
public class ApiPathTests
{
    [DataTestMethod]
    [DataRow(null, "https://cms.test/api/pages/1")]
    [DataRow("", "https://cms.test/api/pages/1")]
    [DataRow("/api/v1", "https://cms.test/api/v1/pages/1")]
    [DataRow("api/v1/", "https://cms.test/api/v1/pages/1")]
    public void BuildUri_RespectsApiPath(string? apiPath, string expected)
    {
        // Arrange
        var creds = new List<AuthenticationCredentialsProvider>
        {
            new(CredNames.BaseUrl, "https://cms.test/"),
            new(CredNames.ApiKey, "users API-Key x")
        };
        if (apiPath != null)
            creds.Add(new(CredNames.ApiPath, apiPath));

        var client = new PayloadRestClient(creds);
        var request = new RestRequest("pages/1");
        
        // Act
        var uri = client.BuildUri(request);

        // Assert
        Assert.AreEqual(expected, uri.ToString());
    }
}