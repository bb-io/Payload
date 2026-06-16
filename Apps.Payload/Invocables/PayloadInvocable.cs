using Apps.Payload.Api;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.Payload.Invocables;

public class PayloadInvocable(InvocationContext invocationContext) : BaseInvocable(invocationContext)
{
    protected IEnumerable<AuthenticationCredentialsProvider> Creds =>
        InvocationContext.AuthenticationCredentialsProviders;

    protected PayloadRestClient Client => new(Creds);
}
