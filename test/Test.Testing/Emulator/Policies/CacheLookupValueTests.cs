// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Testing;
using Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class CacheLookupValueTests
{
    class SimpleCacheLookupValuePreferExternal : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.CacheLookupValue(new CacheLookupValueConfig()
            {
                Key = "key", VariableName = "variable", CachingType = "prefer-external"
            });
        }
    }

    class SimpleCacheLookupValueFromInternal : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.CacheLookupValue(new CacheLookupValueConfig()
            {
                Key = "key", VariableName = "variable", CachingType = "internal"
            });
        }
    }

    class SimpleCacheLookupValueFromExternal : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.CacheLookupValue(new CacheLookupValueConfig()
            {
                Key = "key", VariableName = "variable", CachingType = "external"
            });
        }
    }

    class SimpleCacheLookupValueWithDefaultValue : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.CacheLookupValue(new CacheLookupValueConfig()
            {
                Key = "key", VariableName = "variable", DefaultValue = "test-value"
            });
        }
    }

    [TestMethod]
    public void CacheLookupValue_Callback()
    {
        var test = new SimpleCacheLookupValuePreferExternal().AsTestDocument();
        var executedCallback = false;
        test.SetupInbound().CacheLookupValue().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunInbound();

        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void CacheLookupValue_WithValueCallback()
    {
        var test = new SimpleCacheLookupValuePreferExternal().AsTestDocument();
        test.SetupInbound().CacheLookupValue().WithValue("test");

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("variable").WhoseValue.Should().Be("test");
    }

    [TestMethod]
    public void CacheLookupValue_PreferExternal_SetupCacheStore_WithInternalValue()
    {
        var test = new SimpleCacheLookupValuePreferExternal().AsTestDocument();
        test.SetupCacheStore().WithInternalCacheValue("key", "test");

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("variable").WhoseValue.Should().Be("test");
    }

    [TestMethod]
    public void CacheLookupValue_PreferExternal_SetupCacheStore_WithExternalCacheSetup()
    {
        var test = new SimpleCacheLookupValuePreferExternal().AsTestDocument();
        test.SetupCacheStore().WithExternalCacheSetup().WithExternalCacheValue("key", "test");

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("variable").WhoseValue.Should().Be("test");
    }

    [TestMethod]
    public void CacheLookupValue_WithDefaultValue()
    {
        var test = new SimpleCacheLookupValueWithDefaultValue().AsTestDocument();

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("variable").WhoseValue.Should().Be("test-value");
    }

    [TestMethod]
    public void CacheLookupValue_InternalCache_WillNotFindValueFromExternalCache()
    {
        var test = new SimpleCacheLookupValueFromInternal().AsTestDocument();
        test.SetupCacheStore().WithExternalCacheSetup().WithExternalCacheValue("key", "test");

        test.RunInbound();

        test.Context.Variables.Should().NotContainKey("variable");
    }

    [TestMethod]
    public void CacheLookupValue_Internal_WillNotFindValueInInternalCache()
    {
        var test = new SimpleCacheLookupValueFromInternal().AsTestDocument();

        test.RunInbound();

        test.Context.Variables.Should().NotContainKey("variable");
    }

    [TestMethod]
    public void CacheLookupValue_ExternalCache_WillNotFindValueInInternalCache()
    {
        var test = new SimpleCacheLookupValueFromExternal().AsTestDocument();
        test.SetupCacheStore().WithInternalCacheValue("key", "test");

        test.RunInbound();

        test.Context.Variables.Should().NotContainKey("variable");
    }

    [TestMethod]
    public void CacheLookupValue_ExternalCache_WillNotFindValue_WhenExternalCacheNotSetup()
    {
        var test = new SimpleCacheLookupValueFromExternal().AsTestDocument();
        test.SetupCacheStore().WithExternalCacheValue("key", "test");

        test.RunInbound();

        test.Context.Variables.Should().NotContainKey("variable");
    }
}