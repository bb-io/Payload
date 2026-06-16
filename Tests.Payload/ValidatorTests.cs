using Apps.Payload.Connections;
using Blackbird.Applications.Sdk.Common.Authentication;
using Tests.Payload.Base;

namespace Tests.Payload;

[TestClass]
public class ValidatorTests : TestBase
{
    [TestMethod]
    public async Task ValidateConnection_ValidCredentials_ShouldSucceed()
    {
        var validator = new ConnectionValidator(InvocationContext);

        var result = await validator.ValidateConnection(Credentials, CancellationToken.None);

        Console.WriteLine(result.Message);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public async Task ValidateConnection_InvalidApiKey_ShouldFail()
    {
        var validator = new ConnectionValidator(InvocationContext);
        var badCredentials = Credentials
            .Select(x => new AuthenticationCredentialsProvider(x.KeyName, x.Value + "_invalid"));

        var result = await validator.ValidateConnection(badCredentials, CancellationToken.None);

        Console.WriteLine(result.Message);
        Assert.IsFalse(result.IsValid);
    }
}
