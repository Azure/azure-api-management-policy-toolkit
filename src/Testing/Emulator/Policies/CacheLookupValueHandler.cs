// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class CacheLookupValueHandler : PolicyHandler<CacheLookupValueConfig>
{
    public List<Tuple<
        Func<GatewayContext, CacheLookupValueConfig, bool>,
        object
    >> ValueSetup
    { get; } = new();

    public override string PolicyName => nameof(IInboundContext.CacheLookupValue);

    protected override void Handle(GatewayContext context, CacheLookupValueConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Key);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.VariableName);
        var cancellationToken = HttpPolicyTransport.GetCancellationToken(context);
        cancellationToken.ThrowIfCancellationRequested();

        var fromSetup = ValueSetup.Find(tuple => tuple.Item1(context, config));
        if (fromSetup is not null)
        {
            CachePolicyServices.SetVariable(context, config.VariableName, fromSetup.Item2);
            return;
        }

        var cache = CachePolicyServices.Resolve(context, config.CachingType);
        if (cache is null)
        {
            CachePolicyServices.SetVariable(context, config.VariableName, config.DefaultValue);
            return;
        }

        var pending = cache.GetAsync(config.Key, cancellationToken)
            ?? throw new InvalidOperationException("ICache returned a null lookup task.");
        HttpPolicyTransport.ObserveFault(pending);
        var cached = pending.WaitAsync(cancellationToken).GetAwaiter().GetResult();
        cancellationToken.ThrowIfCancellationRequested();
        CachePolicyServices.SetVariable(context, config.VariableName, cached ?? config.DefaultValue);
    }
}