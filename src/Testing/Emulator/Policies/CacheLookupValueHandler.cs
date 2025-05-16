// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

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
    >> ValueSetup { get; } = new();

    public override string PolicyName => nameof(IInboundContext.CacheLookupValue);

    protected override void Handle(GatewayContext context, CacheLookupValueConfig config)
    {
        var fromSetup = ValueSetup.Find(tuple => tuple.Item1(context, config))?.Item2;
        if (fromSetup is not null)
        {
            context.Variables[config.VariableName] = fromSetup;
            return;
        }

        var cachingType = config.CachingType ?? "prefer-external";


        var store = context.CacheStore.GetCache(cachingType);
        if (store is null)
        {
            return;
        }

        if (store.TryGetValue(config.Key, out var value))
        {
            context.Variables[config.VariableName] = value.Value;
            return;
        }

        if (config.DefaultValue is not null)
        {
            context.Variables[config.VariableName] = config.DefaultValue;
        }
    }
}