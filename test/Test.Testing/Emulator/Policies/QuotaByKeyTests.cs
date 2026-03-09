// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class QuotaByKeyTests
{
    class SimpleQuotaByKey : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.QuotaByKey(new QuotaByKeyConfig
            {
                CounterKey = "ip-based",
                RenewalPeriod = 3600,
                Calls = 1000,
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void QuotaByKey_Inbound_ShouldExecuteWithoutError()
    {
        // Arrange
        var test = new TestDocument(new SimpleQuotaByKey());

        // Act & Assert - no-op should not throw
        test.RunInbound();
    }

    [TestMethod]
    public void QuotaByKey_Inbound_Callback()
    {
        // Arrange
        var test = new TestDocument(new SimpleQuotaByKey());
        var executedCallback = false;

        test.SetupInbound().QuotaByKey().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        // Act
        test.RunInbound();

        // Assert
        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void QuotaByKey_Inbound_CallbackWithPredicate()
    {
        // Arrange
        var test = new TestDocument(new SimpleQuotaByKey());
        var executedCallback = false;

        test.SetupInbound()
            .QuotaByKey((_, config) => config.CounterKey == "ip-based")
            .WithCallback((context, _) =>
            {
                executedCallback = true;
                context.Variables["quota-checked"] = true;
            });

        // Act
        test.RunInbound();

        // Assert
        executedCallback.Should().BeTrue();
        test.Context.Variables.Should().ContainKey("quota-checked")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void QuotaByKey_Inbound_PredicateNotMatching()
    {
        // Arrange
        var test = new TestDocument(new SimpleQuotaByKey());
        var executedCallback = false;

        test.SetupInbound()
            .QuotaByKey((_, config) => config.CounterKey == "other-key")
            .WithCallback((_, _) =>
            {
                executedCallback = true;
            });

        // Act - no-op default should execute since predicate doesn't match
        test.RunInbound();

        // Assert
        executedCallback.Should().BeFalse();
    }
}
