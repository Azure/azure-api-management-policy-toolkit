// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class RetryTests
{
    class SimpleRetry : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.Retry(new RetryConfig
            {
                Condition = true,
                Count = 3,
                Interval = 1,
            }, () =>
            {
                context.SetVariable("retried", true);
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class OutboundRetry : IDocument
    {
        public void Inbound(IInboundContext context) { }
        public void Backend(IBackendContext context) { }

        public void Outbound(IOutboundContext context)
        {
            context.Retry(new RetryConfig
            {
                Condition = true,
                Count = 2,
            }, () =>
            {
                context.SetVariable("outbound-retried", true);
            });
        }

        public void OnError(IOnErrorContext context) { }
    }

    class DynamicRetry : IDocument
    {
        private int _attempts;

        public void Inbound(IInboundContext context)
        {
            context.Retry(new RetryConfig
            {
                Condition = true,
                ConditionEvaluator = () => _attempts < 2,
                Count = 3,
            }, () =>
            {
                _attempts++;
                context.SetVariable("attempts", _attempts);
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class RetryAfterException : IDocument
    {
        private int _attempts;

        public void Inbound(IInboundContext context)
        {
            context.Retry(new RetryConfig
            {
                Condition = true,
                ConditionEvaluator = () => _attempts < 2,
                Count = 3,
            }, () =>
            {
                _attempts++;
                context.SetVariable("attempts", _attempts);
                if (_attempts == 1)
                {
                    throw new InvalidOperationException("fail first");
                }
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void Retry_Inbound_ShouldExecuteSectionOnce()
    {
        // Arrange
        var test = new TestDocument(new SimpleRetry());

        // Act
        test.RunInbound();

        // Assert - section should have been executed
        test.Context.Variables.Should().ContainKey("retried")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void Retry_Inbound_Callback()
    {
        // Arrange
        var test = new TestDocument(new SimpleRetry());
        var executedCallback = false;

        test.SetupInbound().Retry().WithCallback((context, config, section) =>
        {
            executedCallback = true;
            // Execute the section to verify we have access to it
            section();
        });

        // Act
        test.RunInbound();

        // Assert
        executedCallback.Should().BeTrue();
        test.Context.Variables.Should().ContainKey("retried")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void Retry_Inbound_CallbackCanSkipSection()
    {
        // Arrange
        var test = new TestDocument(new SimpleRetry());

        test.SetupInbound().Retry().WithCallback((context, config, section) =>
        {
            // Intentionally do NOT call section()
            context.Variables["skipped"] = true;
        });

        // Act
        test.RunInbound();

        // Assert - section should not have executed
        test.Context.Variables.Should().NotContainKey("retried");
        test.Context.Variables.Should().ContainKey("skipped")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void Retry_Outbound_ShouldExecuteSectionOnce()
    {
        // Arrange
        var test = new TestDocument(new OutboundRetry());

        // Act
        test.RunOutbound();

        // Assert
        test.Context.Variables.Should().ContainKey("outbound-retried")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void Retry_Inbound_CallbackWithPredicate()
    {
        // Arrange
        var test = new TestDocument(new SimpleRetry());
        var executedCallback = false;

        test.SetupInbound()
            .Retry((_, config, _) => config.Count == 3)
            .WithCallback((context, config, section) =>
            {
                executedCallback = true;
                section();
            });

        // Act
        test.RunInbound();

        // Assert
        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void Retry_Inbound_ShouldReevaluateConditionBetweenAttempts()
    {
        var test = new DynamicRetry().AsTestDocument();

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("attempts")
            .WhoseValue.Should().Be(2);
    }

    [TestMethod]
    public void Retry_Inbound_ShouldRetryAfterExceptionWhenConditionStillTrue()
    {
        var test = new RetryAfterException().AsTestDocument();

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("attempts")
            .WhoseValue.Should().Be(2);
    }
}
