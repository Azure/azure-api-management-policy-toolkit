// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Testing;
using Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class CacheLookupValueTests
{
    class SimpleCacheLookupValue : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.CacheLookupValue(new CacheLookupValueConfig() { Key = "key", VariableName = "variable" });
        }
    }

    [TestMethod]
    public void CacheLookupValue_Callback()
    {
        var test = new SimpleCacheLookupValue().AsTestDocument();
        var executedCallback = false;
        test.SetupInbound().CacheLookupValue().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunInbound();

        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void CacheLookupValue_WithValue()
    {
        var test = new SimpleCacheLookupValue().AsTestDocument();
        test.SetupInbound().CacheLookupValue().WithValue("test");

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("variable").WhoseValue.Should().Be("test");
    }

    [TestMethod]
    public void CacheLookupValue_SetupCacheStore_WithInternalValue()
    {
        var test = new SimpleCacheLookupValue().AsTestDocument();
        test.SetupCacheStore().WithInternalCacheValue("key", "test");

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("variable").WhoseValue.Should().Be("test");
    }


    [TestMethod]
    public void CacheLookupValue_SetupCacheStore_WithExternalCacheSetup()
    {
        var test = new SimpleCacheLookupValue().AsTestDocument();
        test.SetupCacheStore().WithExternalCacheSetup().WithExternalCacheValue("key", "test");

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("variable").WhoseValue.Should().Be("test");
    }
}