// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class IpFilterHandler : PolicyHandler<IpFilterConfig>
{
    public List<Tuple<
        Func<GatewayContext, IpFilterConfig, bool>,
        Action<GatewayContext, IpFilterConfig>
    >> OnIpAllowed { get; } = new();

    public List<Tuple<
        Func<GatewayContext, IpFilterConfig, bool>,
        Action<GatewayContext, IpFilterConfig>
    >> OnIpDenied { get; } = new();

    public override string PolicyName => nameof(IInboundContext.IpFilter);

    protected override void Handle(GatewayContext context, IpFilterConfig config)
    {
        if (!IPAddress.TryParse(context.Request.IpAddress, out var clientIp))
        {
            if ("allow".Equals(config.Action, StringComparison.InvariantCultureIgnoreCase))
            {
                DenyAccess(context, config);
            }

            OnIpAllowed.Find(tuple => tuple.Item1(context, config))?.Item2(context, config);
            return;
        }

        var directMatch = (config.Addresses ?? [])
            .Any(address => clientIp.CompareTo(IPAddress.Parse(address)) == 0);
        var rangeMatch = (config.AddressRanges ?? [])
            .Any(range => clientIp.CompareTo(IPAddress.Parse(range.From)) >= 0
                          && clientIp.CompareTo(IPAddress.Parse(range.To)) <= 0);
        var match = directMatch || rangeMatch;

        if ("allow".Equals(config.Action, StringComparison.InvariantCultureIgnoreCase))
        {
            if (!match)
            {
                DenyAccess(context, config);
            }
        }
        else if ("forbid".Equals(config.Action, StringComparison.InvariantCultureIgnoreCase))
        {
            if (match)
            {
                DenyAccess(context, config);
            }
        }
        else
        {
            throw new NotSupportedException("Specified filter action is not supported.");
        }

        OnIpAllowed.Find(tuple => tuple.Item1(context, config))?.Item2(context, config);
    }

    void DenyAccess(GatewayContext context, IpFilterConfig config)
    {
        context.Response = new MockResponse()
        {
            StatusCode = 403,
            StatusReason = "Forbidden", // TODO use code to reason mapper
            Headers = { { "Content-Type", ["application/json"] } },
            Body =
            {
                Content = """
                          {
                            "statusCode": 403,
                            "message": "Forbidden"
                          }
                          """
            }
        };

        OnIpDenied.Find(tuple => tuple.Item1(context, config))?.Item2(context, config);

        throw new FinishSectionProcessingException();
    }
}