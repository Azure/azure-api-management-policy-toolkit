// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class CacheValueTests
{
    class SimpleCacheValueDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.CacheValue(
                new CacheValueConfig
                {
                    Key = "test-key",
                    VariableName = "cachedVar",
                    ExpiresAfter = 60,
                },
                () => { context.SetVariable("cachedVar", "computed-value"); });
        }

        public void Outbound(IOutboundContext context) { }
        public void Backend(IBackendContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class CacheValueWithDefaultDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.CacheValue(
                new CacheValueConfig
                {
                    Key = "test-key",
                    VariableName = "cachedVar",
                    ExpiresAfter = 60,
                    DefaultValue = "default-fallback",
                },
                () => { /* value block does not set the variable */ });
        }

        public void Outbound(IOutboundContext context) { }
        public void Backend(IBackendContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class CacheValueMinimalConfigDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.CacheValue(
                new CacheValueConfig { Key = "minimal-key", VariableName = "minVar" },
                () => { context.SetVariable("minVar", "minimal-value"); });
        }

        public void Outbound(IOutboundContext context) { }
        public void Backend(IBackendContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class OutboundCacheValueDocument : IDocument
    {
        public void Inbound(IInboundContext context) { }

        public void Outbound(IOutboundContext context)
        {
            context.CacheValue(
                new CacheValueConfig
                {
                    Key = "outbound-key",
                    VariableName = "outVar",
                    ExpiresAfter = 30,
                },
                () => { context.SetVariable("outVar", "outbound-value"); });
        }

        public void Backend(IBackendContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void CacheValue_CacheMiss_ExecutesSectionAndSetsVariable()
    {
        var test = new SimpleCacheValueDocument().AsTestDocument();

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("cachedVar")
            .WhoseValue.Should().Be("computed-value");
    }

    [TestMethod]
    public void CacheValue_CacheMiss_StoresValueInCache()
    {
        var test = new SimpleCacheValueDocument().AsTestDocument();
        var cacheStore = test.SetupCacheStore();

        test.RunInbound();

        var cacheValue = cacheStore.InternalCache.Should().ContainKey("test-key").WhoseValue;
        cacheValue.Value.Should().Be("computed-value");
        cacheValue.Ttl.TotalSeconds.Should().Be(60);
    }

    [TestMethod]
    public void CacheValue_CacheHit_SkipsSectionAndSetsVariableFromCache()
    {
        var test = new SimpleCacheValueDocument().AsTestDocument();
        test.SetupCacheStore().WithInternalCacheValue("test-key", "cached-value");

        // The section would set "computed-value", but cache hit should return "cached-value" instead.
        test.RunInbound();

        test.Context.Variables.Should().ContainKey("cachedVar")
            .WhoseValue.Should().Be("cached-value");
    }

    [TestMethod]
    public void CacheValue_CacheHit_DoesNotOverwriteCache()
    {
        var test = new SimpleCacheValueDocument().AsTestDocument();
        var cacheStore = test.SetupCacheStore().WithInternalCacheValue("test-key", "cached-value", 120);

        test.RunInbound();

        cacheStore.InternalCache["test-key"].Value.Should().Be("cached-value");
        cacheStore.InternalCache["test-key"].Ttl.TotalSeconds.Should().Be(120);
    }

    [TestMethod]
    public void CacheValue_DefaultValue_WhenSectionDoesNotSetVariable()
    {
        var test = new CacheValueWithDefaultDocument().AsTestDocument();

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("cachedVar")
            .WhoseValue.Should().Be("default-fallback");
    }

    [TestMethod]
    public void CacheValue_MinimalConfig_WorksWithRequiredPropertiesOnly()
    {
        var test = new CacheValueMinimalConfigDocument().AsTestDocument();

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("minVar")
            .WhoseValue.Should().Be("minimal-value");
    }

    [TestMethod]
    public void CacheValue_Callback_OverridesDefaultBehavior()
    {
        var test = new SimpleCacheValueDocument().AsTestDocument();
        var callbackExecuted = false;

        test.SetupInbound().CacheValue().WithCallback((ctx, config) =>
        {
            callbackExecuted = true;
            ctx.Variables["cachedVar"] = "callback-value";
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Variables.Should().ContainKey("cachedVar")
            .WhoseValue.Should().Be("callback-value");
    }

    [TestMethod]
    public void CacheValue_Outbound_WorksInOutboundSection()
    {
        var test = new OutboundCacheValueDocument().AsTestDocument();

        test.RunOutbound();

        test.Context.Variables.Should().ContainKey("outVar")
            .WhoseValue.Should().Be("outbound-value");
    }
}
