using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Tests.Payload.Base;

public abstract class TestBase
{
    protected IEnumerable<AuthenticationCredentialsProvider> Credentials { get; set; }

    protected InvocationContext InvocationContext { get; set; }

    protected FileManager FileManager { get; set; }

    protected TestBase()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        Credentials = config.GetSection("ConnectionDefinition")
            .GetChildren()
            .Select(x => new AuthenticationCredentialsProvider(x.Key, x.Value ?? string.Empty))
            .ToList();

        InvocationContext = new InvocationContext
        {
            AuthenticationCredentialsProviders = Credentials
        };

        FileManager = new FileManager();
    }

    protected static void PrintJsonResult(object result) =>
        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
}
