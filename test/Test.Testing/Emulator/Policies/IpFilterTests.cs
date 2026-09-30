// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

using Newtonsoft.Json.Linq;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class IpFilterTests
{
    class SimpleAllowAddressIpFilter : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.IpFilter(new IpFilterConfig() { Action = "allow", Addresses = ["192.168.0.64"] });
        }
    }

    class SimpleAllowRangeIpFilter : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.IpFilter(new IpFilterConfig()
            {
                Action = "allow",
                AddressRanges = [new AddressRange() { From = "192.168.0.1", To = "192.168.0.255" }]
            });
        }
    }

    class SimpleForbidAddressIpFilter : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.IpFilter(new IpFilterConfig() { Action = "forbid", Addresses = ["192.168.0.64"] });
        }
    }

    class SimpleForbidRangeIpFilter : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.IpFilter(new IpFilterConfig()
            {
                Action = "forbid",
                AddressRanges = [new AddressRange() { From = "192.168.0.1", To = "192.168.0.255" }]
            });
        }
    }

    [TestMethod]
    public void IpFilter_Callback()
    {
        var test = new SimpleAllowAddressIpFilter().AsTestDocument();
        var executedCallback = false;
        test.SetupInbound().IpFilter().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunInbound();

        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void IpFilter_ShouldAllowRequestIpAddress_AllowSingleAddressFilter()
    {
        var test = new SimpleAllowAddressIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.168.0.64";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().NotBe(403);
    }

    [TestMethod]
    public void IpFilter_ShouldAllowRequestIpAddress_AllowRangeFilter()
    {
        var test = new SimpleAllowRangeIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.168.0.64";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().NotBe(403);
    }

    [TestMethod]
    public void IpFilter_ShouldAllowRequestIpAddress_ForbidSingleAddressFilter()
    {
        var test = new SimpleForbidAddressIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.168.0.65";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().NotBe(403);
    }

    [TestMethod]
    public void IpFilter_ShouldAllowRequestIpAddress_ForbidRangeFilter()
    {
        var test = new SimpleForbidRangeIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.169.0.64";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().NotBe(403);
    }

    [TestMethod]
    public void IpFilter_ShouldDenyRequestIpAddress_AllowSingleAddressFilter()
    {
        var test = new SimpleAllowAddressIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.168.0.65";

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(403);
        response.Headers.Should().ContainKey("Content-Type")
            .WhoseValue.Should().ContainSingle()
            .Which.Should().Be("application/json");
        response.Body.Content.Should().NotBeNullOrWhiteSpace();
        var body = response.Body.As<JObject>();
        body.Should().ContainKey("statusCode")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<int>().Should().Be(403);
        body.Should().ContainKey("message")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<string>().Should().Be("Forbidden");
    }

    [TestMethod]
    public void IpFilter_ShouldDenyRequestIpAddress_AllowRangeFilter()
    {
        var test = new SimpleAllowRangeIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.169.0.64";

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(403);
        response.Headers.Should().ContainKey("Content-Type")
            .WhoseValue.Should().ContainSingle()
            .Which.Should().Be("application/json");
        response.Body.Content.Should().NotBeNullOrWhiteSpace();
        var body = response.Body.As<JObject>();
        body.Should().ContainKey("statusCode")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<int>().Should().Be(403);
        body.Should().ContainKey("message")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<string>().Should().Be("Forbidden");
    }

    [TestMethod]
    public void IpFilter_ShouldDenyRequestIpAddress_ForbidSingleAddressFilter()
    {
        var test = new SimpleForbidAddressIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.168.0.64";

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(403);
        response.Headers.Should().ContainKey("Content-Type")
            .WhoseValue.Should().ContainSingle()
            .Which.Should().Be("application/json");
        response.Body.Content.Should().NotBeNullOrWhiteSpace();
        var body = response.Body.As<JObject>();
        body.Should().ContainKey("statusCode")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<int>().Should().Be(403);
        body.Should().ContainKey("message")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<string>().Should().Be("Forbidden");
    }

    [TestMethod]
    public void IpFilter_ShouldDenyRequestIpAddress_ForbidRangeFilter()
    {
        var test = new SimpleForbidRangeIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.168.0.64";

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(403);
        response.Headers.Should().ContainKey("Content-Type")
            .WhoseValue.Should().ContainSingle()
            .Which.Should().Be("application/json");
        response.Body.Content.Should().NotBeNullOrWhiteSpace();
        var body = response.Body.As<JObject>();
        body.Should().ContainKey("statusCode")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<int>().Should().Be(403);
        body.Should().ContainKey("message")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<string>().Should().Be("Forbidden");
    }

    [TestMethod]
    [DataRow("192.0.2.64", "192.0.2.64", true)]
    [DataRow("192.0.2.65", "192.0.2.64", false)]
    [DataRow("0.0.0.0", "0.0.0.0", true)]
    [DataRow("255.255.255.255", "255.255.255.255", true)]
    [DataRow("2001:db8::1", "2001:0DB8:0000:0000:0000:0000:0000:0001", true)]
    [DataRow("2001:db8::2", "2001:db8::1", false)]
    [DataRow("::", "::", true)]
    [DataRow("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", true)]
    [DataRow("192.0.2.64", "::ffff:192.0.2.64", true)]
    [DataRow("::ffff:192.0.2.64", "192.0.2.64", true)]
    [DataRow("::ffff:c000:240", "192.0.2.64", true)]
    [DataRow("::192.0.2.64", "192.0.2.64", false)]
    [DataRow("::", "0.0.0.0", false)]
    public void IpFilter_AddressMatchingHasExactAllowAndForbidSemantics(
        string client, string address, bool matches)
    {
        foreach (var action in new[] { "allow", "forbid", "ALLOW", "FORBID" })
        {
            var test = CreateTest(new IpFilterConfig { Action = action, Addresses = [address] });
            SeedContext(test.Context, client);
            var response = test.Context.Response;
            var headers = response.Headers;
            var body = response.Body;

            test.RunInbound();

            AssertDecision(test.Context, action.Equals("allow", StringComparison.OrdinalIgnoreCase) == matches);
            test.Context.Response.Should().BeSameAs(response);
            response.Headers.Should().BeSameAs(headers);
            response.Body.Should().BeSameAs(body);
            AssertRequestUnchanged(test.Context, client);
        }
    }

    [TestMethod]
    [DataRow("192.0.2.9", "192.0.2.10", "192.0.2.20", false)]
    [DataRow("192.0.2.10", "192.0.2.10", "192.0.2.20", true)]
    [DataRow("192.0.2.15", "192.0.2.10", "192.0.2.20", true)]
    [DataRow("192.0.2.20", "192.0.2.10", "192.0.2.20", true)]
    [DataRow("192.0.2.21", "192.0.2.10", "192.0.2.20", false)]
    [DataRow("192.0.2.10", "192.0.2.10", "192.0.2.10", true)]
    [DataRow("192.0.3.0", "192.0.2.255", "192.0.3.1", true)]
    [DataRow("0.0.0.0", "0.0.0.0", "255.255.255.255", true)]
    [DataRow("255.255.255.255", "0.0.0.0", "255.255.255.255", true)]
    [DataRow("2001:db8::fe", "2001:db8::ff", "2001:db8::101", false)]
    [DataRow("2001:db8::ff", "2001:db8::ff", "2001:db8::101", true)]
    [DataRow("2001:db8::100", "2001:db8::ff", "2001:db8::101", true)]
    [DataRow("2001:db8::101", "2001:db8::ff", "2001:db8::101", true)]
    [DataRow("2001:db8::102", "2001:db8::ff", "2001:db8::101", false)]
    [DataRow("::", "::", "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", true)]
    [DataRow("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", "::", "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", true)]
    [DataRow("192.0.2.15", "::ffff:192.0.2.10", "::ffff:192.0.2.20", true)]
    [DataRow("::ffff:192.0.2.15", "192.0.2.10", "192.0.2.20", true)]
    [DataRow("192.0.2.10", "192.0.2.10", "::ffff:192.0.2.20", true)]
    [DataRow("::ffff:192.0.2.20", "::ffff:192.0.2.10", "192.0.2.20", true)]
    [DataRow("192.0.2.9", "::ffff:192.0.2.10", "192.0.2.20", false)]
    [DataRow("192.0.2.21", "192.0.2.10", "::ffff:192.0.2.20", false)]
    [DataRow("192.0.2.15", "::", "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", false)]
    [DataRow("2001:db8::1", "0.0.0.0", "255.255.255.255", false)]
    public void IpFilter_RangesUseInclusiveBoundariesAndCompatibleAddressFamilies(
        string client, string from, string to, bool matches)
    {
        foreach (var action in new[] { "allow", "forbid" })
        {
            var test = CreateTest(new IpFilterConfig
            {
                Action = action, AddressRanges = [new AddressRange { From = from, To = to }]
            });
            SeedContext(test.Context, client);

            test.RunInbound();

            AssertDecision(test.Context, (action == "allow") == matches);
            AssertRequestUnchanged(test.Context, client);
        }
    }

    [TestMethod]
    [DataRow("192.0.2.10", true)]
    [DataRow("192.0.2.20", true)]
    [DataRow("192.0.2.30", true)]
    [DataRow("192.0.2.31", false)]
    public void IpFilter_CombinesAddressesAndRanges(string client, bool matches)
    {
        foreach (var action in new[] { "allow", "forbid" })
        {
            var test = CreateTest(new IpFilterConfig
            {
                Action = action,
                Addresses = ["2001:db8::1", "192.0.2.10"],
                AddressRanges = [new AddressRange { From = "192.0.2.20", To = "192.0.2.30" }]
            });
            SeedContext(test.Context, client);

            test.RunInbound();

            AssertDecision(test.Context, (action == "allow") == matches);
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("deny")]
    [DataRow("permit")]
    [DataRow("allow ")]
    public void IpFilter_RejectsInvalidActionsEvenWhenClientAddressIsInvalid(string? action)
    {
        foreach (var client in new[] { "192.0.2.10", "not-an-ip" })
        {
            var test = CreateTest(new IpFilterConfig { Action = action!, Addresses = ["192.0.2.10"] });
            SeedContext(test.Context, client);

            var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

            AssertPolicyError(error);
            error.InnerException.Should().BeAssignableTo<ArgumentException>()
                .Which.ParamName.Should().Be(nameof(IpFilterConfig.Action));
            AssertOriginalContext(test.Context, client);
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("not-an-ip")]
    [DataRow("256.0.0.1")]
    [DataRow("2001:::1")]
    public void IpFilter_RejectsInvalidClientAddresses(string? client)
    {
        foreach (var action in new[] { "allow", "forbid" })
        {
            var test = CreateTest(new IpFilterConfig { Action = action, Addresses = ["192.0.2.10"] });
            SeedContext(test.Context, client!);
            test.SetupInbound().IpFilter().OnIpAllow((_, _) => Assert.Fail("Allow hook ran for invalid input."));
            test.SetupInbound().IpFilter().OnIpDeny((_, _) => Assert.Fail("Deny hook ran for invalid input."));

            var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

            AssertPolicyError(error);
            error.InnerException.Should().BeAssignableTo<ArgumentException>()
                .Which.ParamName.Should().Be(nameof(test.Context.Request.IpAddress));
            AssertOriginalContext(test.Context, client!);
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("not-an-ip")]
    [DataRow("256.0.0.1")]
    [DataRow("2001:::1")]
    public void IpFilter_RejectsInvalidConfiguredAddresses(string? address)
    {
        foreach (var action in new[] { "allow", "forbid" })
        {
            var test = CreateTest(new IpFilterConfig { Action = action, Addresses = [address!] });
            SeedContext(test.Context, "192.0.2.10");

            var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

            AssertPolicyError(error);
            error.InnerException.Should().BeAssignableTo<ArgumentException>();
            AssertOriginalContext(test.Context, "192.0.2.10");
        }
    }

    [TestMethod]
    [DataRow(null, "192.0.2.20")]
    [DataRow("192.0.2.10", null)]
    [DataRow("invalid", "192.0.2.20")]
    [DataRow("192.0.2.10", "invalid")]
    [DataRow("192.0.2.20", "192.0.2.10")]
    [DataRow("2001:db8::20", "2001:db8::10")]
    [DataRow("::ffff:192.0.2.20", "192.0.2.10")]
    [DataRow("192.0.2.10", "2001:db8::20")]
    [DataRow("::", "192.0.2.20")]
    public void IpFilter_RejectsInvalidOrReversedOrIncompatibleRanges(string? from, string? to)
    {
        foreach (var action in new[] { "allow", "forbid" })
        {
            var test = CreateTest(new IpFilterConfig
            {
                Action = action, AddressRanges = [new AddressRange { From = from!, To = to! }]
            });
            SeedContext(test.Context, "192.0.2.15");

            var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

            AssertPolicyError(error);
            error.InnerException.Should().BeAssignableTo<ArgumentException>();
            AssertOriginalContext(test.Context, "192.0.2.15");
        }
    }

    [TestMethod]
    public void IpFilter_RejectsNullRangeEntries()
    {
        var test = CreateTest(new IpFilterConfig { Action = "allow", AddressRanges = [null!] });
        SeedContext(test.Context, "192.0.2.10");

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        AssertPolicyError(error);
        error.InnerException.Should().BeOfType<ArgumentNullException>();
        AssertOriginalContext(test.Context, "192.0.2.10");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void IpFilter_RequiresAtLeastOneAddressOrRange(bool emptyAddresses, bool emptyRanges)
    {
        foreach (var action in new[] { "allow", "forbid" })
        {
            var test = CreateTest(new IpFilterConfig
            {
                Action = action,
                Addresses = emptyAddresses ? [] : null,
                AddressRanges = emptyRanges ? [] : null
            });
            SeedContext(test.Context, "192.0.2.10");

            var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

            AssertPolicyError(error);
            error.InnerException.Should().BeAssignableTo<ArgumentException>();
            AssertOriginalContext(test.Context, "192.0.2.10");
        }
    }

    [TestMethod]
    [DataRow("allow", "addresses")]
    [DataRow("forbid", "addresses")]
    [DataRow("allow", "ranges")]
    [DataRow("forbid", "ranges")]
    [DataRow("allow", "reversed-range")]
    [DataRow("forbid", "reversed-range")]
    public void IpFilter_ValidatesEveryEntryBeforeApplyingAMatchingRule(string action, string invalidEntry)
    {
        var config = new IpFilterConfig
        {
            Action = action,
            Addresses = invalidEntry == "addresses" ? ["192.0.2.10", "invalid"] : ["192.0.2.10"],
            AddressRanges = invalidEntry switch
            {
                "addresses" => null,
                "ranges" =>
                [
                    new AddressRange { From = "192.0.2.1", To = "192.0.2.20" },
                    new AddressRange { From = "invalid", To = "192.0.2.20" }
                ],
                "reversed-range" =>
                [
                    new AddressRange { From = "192.0.2.1", To = "192.0.2.20" },
                    new AddressRange { From = "192.0.2.20", To = "192.0.2.1" }
                ],
                _ => throw new ArgumentOutOfRangeException(nameof(invalidEntry))
            }
        };
        var test = CreateTest(config);
        SeedContext(test.Context, "192.0.2.10");
        test.SetupInbound().IpFilter().OnIpAllow((_, _) => Assert.Fail("Allow hook ran before validation."));
        test.SetupInbound().IpFilter().OnIpDeny((_, _) => Assert.Fail("Deny hook ran before validation."));

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        AssertPolicyError(error);
        error.InnerException.Should().BeAssignableTo<ArgumentException>();
        AssertOriginalContext(test.Context, "192.0.2.10");
    }

    [TestMethod]
    public void IpFilter_CallbackOverridesDefaultValidationAndDecision()
    {
        var config = new IpFilterConfig { Action = "invalid", Addresses = ["invalid"] };
        var test = CreateTest(config);
        SeedContext(test.Context, "invalid-client");
        test.SetupInbound().IpFilter().OnIpAllow((_, _) => Assert.Fail("Default allow hook ran."));
        test.SetupInbound().IpFilter().OnIpDeny((_, _) => Assert.Fail("Default deny hook ran."));
        test.SetupInbound().IpFilter((context, candidate) => context == test.Context && candidate == config)
            .WithCallback((context, candidate) =>
            {
                candidate.Should().BeSameAs(config);
                context.Variables["override"] = true;
            });

        test.RunInbound();

        test.Context.Variables["override"].Should().Be(true);
        test.Context.Variables["after-ip-filter"].Should().Be(true);
        AssertAllowedResponse(test.Context);
    }

    [TestMethod]
    [DataRow("allow", "192.0.2.10", true)]
    [DataRow("allow", "192.0.2.11", false)]
    [DataRow("forbid", "192.0.2.11", true)]
    [DataRow("forbid", "192.0.2.10", false)]
    public void IpFilter_UsesOnlyFirstMatchingOutcomeHook(string action, string client, bool allowed)
    {
        var config = new IpFilterConfig { Action = action, Addresses = ["192.0.2.10"] };
        var test = CreateTest(config);
        SeedContext(test.Context, client);
        var callbacks = new List<string>();
        test.SetupInbound().IpFilter((_, _) => false).OnIpAllow((_, _) => Assert.Fail("Nonmatching allow hook ran."));
        test.SetupInbound().IpFilter((_, _) => false).OnIpDeny((_, _) => Assert.Fail("Nonmatching deny hook ran."));
        var setup = test.SetupInbound().IpFilter((context, candidate) => context == test.Context && candidate == config);
        setup.OnIpAllow((context, candidate) =>
        {
            allowed.Should().BeTrue();
            candidate.Should().BeSameAs(config);
            AssertAllowedResponse(context);
            callbacks.Add("allow");
            context.Variables["outcome"] = "allow";
        });
        setup.OnIpDeny((context, candidate) =>
        {
            allowed.Should().BeFalse();
            candidate.Should().BeSameAs(config);
            AssertForbiddenResponse(context);
            callbacks.Add("deny");
            context.Variables["outcome"] = "deny";
        });
        test.SetupInbound().IpFilter().OnIpAllow((_, _) => Assert.Fail("Later allow hook ran."));
        test.SetupInbound().IpFilter().OnIpDeny((_, _) => Assert.Fail("Later deny hook ran."));

        test.RunInbound();

        callbacks.Should().Equal(allowed ? "allow" : "deny");
        test.Context.Variables["outcome"].Should().Be(allowed ? "allow" : "deny");
        AssertDecision(test.Context, allowed);
    }

    [TestMethod]
    [DataRow("override")]
    [DataRow("allow")]
    [DataRow("deny")]
    public void IpFilter_CallbackFailuresPropagateWithPolicyMetadata(string hook)
    {
        var test = CreateTest(new IpFilterConfig { Action = "allow", Addresses = ["192.0.2.10"] });
        SeedContext(test.Context, hook == "deny" ? "192.0.2.11" : "192.0.2.10");
        var failure = new InvalidOperationException("IP filter callback failed");
        var setup = test.SetupInbound().IpFilter();
        switch (hook)
        {
            case "override": setup.WithCallback((_, _) => throw failure); break;
            case "allow": setup.OnIpAllow((_, _) => throw failure); break;
            case "deny": setup.OnIpDeny((_, _) => throw failure); break;
            default: throw new ArgumentOutOfRangeException(nameof(hook));
        }

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        AssertPolicyError(error);
        error.InnerException.Should().BeSameAs(failure);
        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Variables.Should().NotContainKey("after-ip-filter");
    }

    [TestMethod]
    public void IpFilter_CallbackCanSimulatePipelineRejection()
    {
        var test = CreateTest(new IpFilterConfig { Action = "allow", Addresses = ["192.0.2.10"] });
        test.SetupInbound().IpFilter().WithCallback((context, _) =>
        {
            context.Response.StatusCode = 429;
            context.Response.StatusReason = "Too Many Requests";
            context.Response.Body.Content = "callback rejection";
            throw new FinishSectionProcessingException();
        });

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.Response.StatusReason.Should().Be("Too Many Requests");
        test.Context.Response.Body.Content.Should().Be("callback rejection");
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after-ip-filter");
    }

    [TestMethod]
    [DataRow("allow", "192.0.2.10", true)]
    [DataRow("allow", "192.0.2.25", true)]
    [DataRow("allow", "192.0.2.31", false)]
    [DataRow("forbid", "192.0.2.10", false)]
    [DataRow("forbid", "192.0.2.25", false)]
    [DataRow("forbid", "192.0.2.31", true)]
    public void IpFilter_ResolvesActionAddressesAndRangeEndpointsFromSharedExpressionContext(
        string action, string client, bool allowed)
    {
        var document = new PolicyDocument
        {
            InboundAction = context =>
            {
                var expression = context.ExpressionContext;
                context.IpFilter(new IpFilterConfig
                {
                    Action = (string)expression.Variables["action"],
                    Addresses = [expression.Request.Headers["X-Allowed-Address"][0]],
                    AddressRanges =
                    [
                        new AddressRange
                        {
                            From = (string)expression.Variables["range-from"],
                            To = (string)expression.Variables["range-to"]
                        }
                    ]
                });
                context.SetVariable("after-ip-filter", true);
            }
        };
        var test = document.AsTestDocument();
        SeedContext(test.Context, client);
        test.Context.Request.Headers["X-Allowed-Address"] = ["192.0.2.10"];
        test.Context.Variables["action"] = action;
        test.Context.Variables["range-from"] = "192.0.2.20";
        test.Context.Variables["range-to"] = "::ffff:192.0.2.30";
        test.SetupInbound().IpFilter((context, config) =>
                context == test.Context && config.Action == action && config.Addresses![0] == "192.0.2.10")
            .OnIpAllow((context, _) => context.Variables["expression-hook"] = "allow");
        test.SetupInbound().IpFilter().OnIpDeny((context, _) => context.Variables["expression-hook"] = "deny");

        test.RunInbound();

        AssertDecision(test.Context, allowed);
        test.Context.Variables["expression-hook"].Should().Be(allowed ? "allow" : "deny");
        test.Context.Variables["action"].Should().Be(action);
        test.Context.Variables["range-from"].Should().Be("192.0.2.20");
        test.Context.Variables["range-to"].Should().Be("::ffff:192.0.2.30");
        test.Context.Request.Headers["X-Allowed-Address"].Should().Equal("192.0.2.10");
        AssertRequestUnchanged(test.Context, client);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void IpFilter_RejectionTerminatesCoordinatedScopesAndSections(bool nested)
    {
        var filter = CreateDocument(new IpFilterConfig { Action = "allow", Addresses = ["192.0.2.10"] });
        var outer = new PolicyDocument
        {
            InboundAction = context =>
            {
                context.SetVariable("outer-before", true);
                context.Base();
                context.SetVariable("outer-after", true);
            },
            BackendAction = context => context.SetVariable("backend", true),
            OutboundAction = context => context.SetVariable("outbound", true),
            OnErrorAction = context => context.SetVariable("on-error", true)
        };
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, nested ? outer : filter)
            .AddPolicy(PolicyScope.Operation, nested ? filter : outer)
            .ConfigureContext(context => SeedContext(context, "192.0.2.11"))
            .Build();

        if (nested) pipeline.RunAllNested();
        else pipeline.RunAll();
        pipeline.RunAll();
        pipeline.RunAllNested();
        pipeline.RunOnError();
        pipeline.RunOnErrorNested();

        AssertDecision(pipeline.Context, false);
        pipeline.Context.Variables.Keys.Should().BeEquivalentTo(
            nested ? new[] { "keep", "outer-before" } : ["keep"]);
    }

    [TestMethod]
    [DataRow("allow", false)]
    [DataRow("allow", true)]
    [DataRow("forbid", false)]
    [DataRow("forbid", true)]
    public void IpFilter_DeniedHookPromotesSectionOnlyInvokeRequestToPipelineTermination(
        string action, bool nested)
    {
        var client = action == "allow" ? "192.0.2.11" : "192.0.2.10";
        Action invokeRequest = () => Assert.Fail("The rejection section was not entered.");
        var filter = new PolicyDocument
        {
            InboundAction = context =>
            {
                invokeRequest = () => context.InvokeRequest(new InvokeRequestConfig());
                context.IpFilter(new IpFilterConfig { Action = action, Addresses = ["192.0.2.10"] });
                context.SetVariable("after-ip-filter", true);
            },
            BackendAction = context => context.SetVariable("filter-backend", true),
            OutboundAction = context => context.SetVariable("filter-outbound", true),
            OnErrorAction = context => context.SetVariable("filter-on-error", true)
        };
        var outer = new PolicyDocument
        {
            InboundAction = context =>
            {
                context.SetVariable("outer-before", true);
                context.Base();
                context.SetVariable("outer-after", true);
            },
            BackendAction = context => context.SetVariable("outer-backend", true),
            OutboundAction = context => context.SetVariable("outer-outbound", true),
            OnErrorAction = context => context.SetVariable("outer-on-error", true)
        };
        var later = new PolicyDocument
        {
            InboundAction = context => context.SetVariable("later-inbound", true),
            BackendAction = context => context.SetVariable("later-backend", true),
            OutboundAction = context => context.SetVariable("later-outbound", true),
            OnErrorAction = context => context.SetVariable("later-on-error", true)
        };
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, nested ? outer : filter)
            .AddPolicy(PolicyScope.Api, nested ? filter : later)
            .AddPolicy(PolicyScope.Operation, later)
            .ConfigureContext(context => SeedContext(context, client))
            .Build();
        var response = pipeline.Context.Response;
        var headers = response.Headers;
        var body = response.Body;
        var setup = new TestDocument(filter) { Context = pipeline.Context };
        var invokeCalls = 0;
        setup.SetupInbound().InvokeRequest().WithCallback((_, _) => invokeCalls++);
        setup.SetupInbound().IpFilter().OnIpAllow((_, _) => Assert.Fail("Allow hook ran for a rejected request."));
        setup.SetupInbound().IpFilter().OnIpDeny((context, _) =>
        {
            context.Variables["denial-hook"] = true;
            AssertForbiddenResponse(context);
            invokeRequest();
            context.Variables["after-invoke-request"] = true;
        });

        if (nested) pipeline.RunAllNested();
        else pipeline.RunAll();
        pipeline.RunAll();
        pipeline.RunAllNested();
        pipeline.RunOnError();
        pipeline.RunOnErrorNested();

        invokeCalls.Should().Be(1);
        AssertDecision(pipeline.Context, false);
        pipeline.Context.Variables.Keys.Should().BeEquivalentTo(
            nested ? new[] { "keep", "outer-before", "denial-hook" } : ["keep", "denial-hook"]);
        pipeline.Context.Response.Should().BeSameAs(response);
        response.Headers.Should().BeSameAs(headers);
        response.Body.Should().BeSameAs(body);
        AssertRequestUnchanged(pipeline.Context, client);
    }

    [TestMethod]
    [DataRow("allow", "192.0.2.10", true)]
    [DataRow("allow", "192.0.2.11", false)]
    [DataRow("forbid", "192.0.2.11", true)]
    [DataRow("forbid", "192.0.2.10", false)]
    public void IpFilter_OnlyAllowedRequestsReachMockResponseOnTheSameContext(
        string action, string client, bool allowed)
    {
        foreach (var nested in new[] { false, true })
        {
            var filter = new PolicyDocument
            {
                InboundAction = context =>
                {
                    context.IpFilter(new IpFilterConfig { Action = action, Addresses = ["192.0.2.10"] });
                    context.SetVariable("ip-filter-allowed", true);
                    context.Base();
                },
                BackendAction = context => context.SetVariable("backend", true),
                OutboundAction = context => context.SetVariable("outbound", true)
            };
            var mock = new PolicyDocument
            {
                InboundAction = context =>
                {
                    context.MockResponse(new MockResponseConfig { StatusCode = 201, ContentType = "application/json" });
                    context.SetVariable("after-mock-response", true);
                }
            };
            var pipeline = PolicyPipelineBuilder.Create()
                .AddPolicy(PolicyScope.Global, filter)
                .AddPolicy(PolicyScope.Operation, mock)
                .ConfigureContext(context => SeedContext(context, client))
                .Build();
            var setup = new TestDocument(mock) { Context = pipeline.Context };
            setup.SetupResponseExampleStore().Add(pipeline.Context,
                new ResponseExample(201, "caf\u00e9", "application/json"));

            if (nested) pipeline.RunAllNested();
            else pipeline.RunAll();

            pipeline.Context.ResponseTerminated.Should().BeTrue();
            pipeline.Context.Variables.Should().NotContainKey("backend")
                .And.NotContainKey("outbound").And.NotContainKey("after-mock-response");
            if (allowed)
            {
                pipeline.Context.Variables["ip-filter-allowed"].Should().Be(true);
                pipeline.Context.Response.StatusCode.Should().Be(201);
                pipeline.Context.Response.StatusReason.Should().Be("Created");
                pipeline.Context.Response.Body.Content.Should().Be("caf\u00e9");
                pipeline.Context.Response.Headers.Should().HaveCount(2);
                pipeline.Context.Response.Headers["Content-Type"].Should().Equal("application/json");
                pipeline.Context.Response.Headers["Content-Length"].Should().Equal("5");
            }
            else
            {
                pipeline.Context.Variables.Should().NotContainKey("ip-filter-allowed");
                AssertForbiddenResponse(pipeline.Context);
            }
            AssertRequestUnchanged(pipeline.Context, client);
        }
    }

    [TestMethod]
    public void IpFilter_IsRegisteredOnlyForInbound()
    {
        typeof(IInboundContext).GetMethods().Should().Contain(method => method.Name == nameof(IInboundContext.IpFilter));
        foreach (var section in new[] { typeof(IBackendContext), typeof(IOutboundContext), typeof(IOnErrorContext) })
        {
            section.GetMethods().Should().NotContain(method => method.Name == nameof(IInboundContext.IpFilter));
        }
        var handler = typeof(GatewayContext).Assembly.GetType(
            "Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies.IpFilterHandler", throwOnError: true)!;
        handler.CustomAttributes.Where(attribute => attribute.AttributeType.Name == "SectionAttribute")
            .Select(attribute => attribute.ConstructorArguments[0].Value)
            .Should().ContainSingle().Which.Should().Be(nameof(IInboundContext));
    }

    private const string ForbiddenBody = """
                                        {
                                          "statusCode": 403,
                                          "message": "Forbidden"
                                        }
                                        """;

    private static TestDocument CreateTest(IpFilterConfig config) => CreateDocument(config).AsTestDocument();

    private static PolicyDocument CreateDocument(IpFilterConfig config) => new()
    {
        InboundAction = context =>
        {
            context.IpFilter(config);
            context.SetVariable("after-ip-filter", true);
        }
    };

    private static void SeedContext(GatewayContext context, string client)
    {
        context.Request.IpAddress = client;
        context.Request.Headers["X-Request"] = ["keep"];
        context.Request.Body.Content = "request body";
        context.Variables["keep"] = "value";
        context.Response.StatusCode = 202;
        context.Response.StatusReason = "Accepted";
        context.Response.Body.Content = "existing response";
        context.Response.Headers = new Dictionary<string, string[]>
        {
            ["content-type"] = ["text/stale"],
            ["CONTENT-LENGTH"] = ["999"],
            ["X-Stale"] = ["discard"]
        };
    }

    private static void AssertRequestUnchanged(GatewayContext context, string client)
    {
        context.Request.IpAddress.Should().Be(client);
        context.Request.Headers["X-Request"].Should().Equal("keep");
        context.Request.Body.Content.Should().Be("request body");
        context.Variables["keep"].Should().Be("value");
    }

    private static void AssertAllowedResponse(GatewayContext context)
    {
        context.Response.StatusCode.Should().Be(202);
        context.Response.StatusReason.Should().Be("Accepted");
        context.Response.Body.Content.Should().Be("existing response");
        context.Response.Headers.Should().HaveCount(3);
        context.Response.Headers["content-type"].Should().Equal("text/stale");
        context.Response.Headers["CONTENT-LENGTH"].Should().Equal("999");
        context.Response.Headers["X-Stale"].Should().Equal("discard");
        context.ResponseTerminated.Should().BeFalse();
    }

    private static void AssertForbiddenResponse(GatewayContext context)
    {
        context.Response.StatusCode.Should().Be(403);
        context.Response.StatusReason.Should().Be("Forbidden");
        context.Response.Body.Content.Should().Be(ForbiddenBody);
        context.Response.Headers.Should().HaveCount(2);
        context.Response.Headers["Content-Type"].Should().Equal("application/json");
        context.Response.Headers["Content-Length"].Should().Equal(
            Encoding.UTF8.GetByteCount(ForbiddenBody).ToString(CultureInfo.InvariantCulture));
    }

    private static void AssertDecision(GatewayContext context, bool allowed)
    {
        if (allowed)
        {
            AssertAllowedResponse(context);
            context.Variables["after-ip-filter"].Should().Be(true);
        }
        else
        {
            AssertForbiddenResponse(context);
            context.ResponseTerminated.Should().BeTrue();
            context.Variables.Should().NotContainKey("after-ip-filter");
        }
    }

    private static void AssertOriginalContext(GatewayContext context, string client)
    {
        AssertAllowedResponse(context);
        AssertRequestUnchanged(context, client);
        context.Variables.Keys.Should().Equal("keep");
    }

    private static void AssertPolicyError(PolicyException error)
    {
        error.Policy.Should().Be(nameof(IInboundContext.IpFilter));
        error.Section.Should().Be(nameof(IInboundContext));
        error.Message.Should().NotBeNullOrWhiteSpace();
    }

    private sealed class PolicyDocument : IDocument
    {
        public Action<IInboundContext>? InboundAction { get; init; }
        public Action<IBackendContext>? BackendAction { get; init; }
        public Action<IOutboundContext>? OutboundAction { get; init; }
        public Action<IOnErrorContext>? OnErrorAction { get; init; }

        public void Inbound(IInboundContext context) => InboundAction?.Invoke(context);
        public void Backend(IBackendContext context) => BackendAction?.Invoke(context);
        public void Outbound(IOutboundContext context) => OutboundAction?.Invoke(context);
        public void OnError(IOnErrorContext context) => OnErrorAction?.Invoke(context);
    }
}