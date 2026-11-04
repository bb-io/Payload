using System.Net;
using Apps.Payload.Api;
using Apps.Payload.Constants;
using Blackbird.Applications.Sdk.Common.Authentication;
using RestSharp;

namespace Tests.Payload.Unit;

[TestClass]
public class ErrorMessageTests
{
    private class ErrorExposingClient() : PayloadRestClient(
    [
        new AuthenticationCredentialsProvider(CredNames.BaseUrl, "https://cms.test"),
        new AuthenticationCredentialsProvider(CredNames.ApiKey, "users API-Key x")
    ])
    {
        public Exception ToException(string content) =>
            ConfigureErrorException(new RestResponse(new RestRequest("articles/7"))
            {
                Content = content,
                StatusCode = HttpStatusCode.BadRequest
            });
    }

    [TestMethod]
    public void ValidationError_ListsEveryInvalidFieldWithPathAndReason()
    {
        const string content = """
            {
                "errors": [
                    {
                        "name": "ValidationError",
                        "message": "The following fields are invalid: Article > Content, Title",
                        "data": {
                            "collection": "articles",
                            "errors": [
                                { "label": "Article > Content", "message": "This field is required.", "path": "article.content" },
                                { "message": "This field is required.", "path": "title", "locale": "es" }
                            ]
                        }
                    }
                ]
            }
            """;

        var message = new ErrorExposingClient().ToException(content).Message;

        StringAssert.StartsWith(message, "The following fields are invalid: Article > Content, Title");
        StringAssert.Contains(message, "article.content: This field is required.");
        StringAssert.Contains(message, "[es] title: This field is required.");
    }

    [TestMethod]
    public void SeveralErrors_AreAllReported()
    {
        const string content = """{ "errors": [ { "message": "First problem" }, { "message": "Second problem" } ] }""";

        var message = new ErrorExposingClient().ToException(content).Message;

        Assert.AreEqual("First problem; Second problem", message);
    }

    [TestMethod]
    public void ErrorWithoutDetails_KeepsPayloadMessage()
    {
        const string content = """{ "errors": [ { "message": "You are not allowed to perform this action." } ] }""";

        var message = new ErrorExposingClient().ToException(content).Message;

        Assert.AreEqual("You are not allowed to perform this action.", message);
    }
}
