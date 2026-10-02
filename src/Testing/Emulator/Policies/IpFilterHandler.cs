// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

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
        var allow = string.Equals(config.Action, "allow", StringComparison.OrdinalIgnoreCase);
        if (!allow && !string.Equals(config.Action, "forbid", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The IP filter action must be 'allow' or 'forbid'.", nameof(config.Action));
        }

        var addresses = (config.Addresses ?? [])
            .Select(address => ParseAddress(address, nameof(config.Addresses))).ToArray();
        var ranges = (config.AddressRanges ?? []).Select(range =>
        {
            ArgumentNullException.ThrowIfNull(range);
            var from = ParseAddress(range.From, nameof(range.From));
            var to = ParseAddress(range.To, nameof(range.To));
            if (from.AddressFamily != to.AddressFamily)
            {
                throw new ArgumentException("IP address range endpoints must use compatible address families.",
                    nameof(config.AddressRanges));
            }
            if (from.CompareTo(to) > 0)
            {
                throw new ArgumentException("An IP address range must start at or before its ending address.",
                    nameof(config.AddressRanges));
            }

            return (From: from, To: to);
        }).ToArray();
        if (addresses.Length == 0 && ranges.Length == 0)
        {
            throw new ArgumentException("The IP filter requires at least one address or address range.",
                nameof(config));
        }

        var clientIp = ParseAddress(context.Request.IpAddress, nameof(context.Request.IpAddress));
        var match = addresses.Any(address => clientIp.Equals(address)) ||
                    ranges.Any(range => clientIp.AddressFamily == range.From.AddressFamily &&
                                        clientIp.CompareTo(range.From) >= 0 &&
                                        clientIp.CompareTo(range.To) <= 0);
        if (allow != match)
        {
            DenyAccess(context, config);
        }

        OnIpAllowed.Find(tuple => tuple.Item1(context, config))?.Item2(context, config);
    }

    private static IPAddress ParseAddress(string address, string parameterName)
    {
        if (!IPAddress.TryParse(address, out var parsed))
        {
            throw new ArgumentException($"'{address}' is not a valid IP address.", parameterName);
        }

        return parsed.IsIPv4MappedToIPv6 ? parsed.MapToIPv4() : parsed;
    }

    private void DenyAccess(GatewayContext context, IpFilterConfig config)
    {
        ResponseUtilities.Overwrite(context.Response, 403, "Forbidden");
        context.Response.Headers["Content-Type"] = ["application/json"];
        context.Response.Body.Content = """
                                       {
                                         "statusCode": 403,
                                         "message": "Forbidden"
                                       }
                                       """;
        context.Response.Headers["Content-Length"] =
            [Encoding.UTF8.GetByteCount(context.Response.Body.Content).ToString(CultureInfo.InvariantCulture)];

        try
        {
            OnIpDenied.Find(tuple => tuple.Item1(context, config))?.Item2(context, config);
        }
        catch (FinishSectionProcessingException termination)
        {
            termination.TerminatesPipeline = true;
            context.RecordTermination(termination);
            throw;
        }

        context.ResponseTerminated = true;
        throw new FinishSectionProcessingException();
    }
}
