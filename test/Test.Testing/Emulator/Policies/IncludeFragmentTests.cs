// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class IncludeFragmentTests
{
    [Document("default-value-fragment")]
    public class ValueFragment : IFragment
    {
        [FragmentVariable(DefaultValue = "fallback")]
        public string? Value { get; set; }

        public void Fragment(IFragmentContext context) => context.SetHeader("X-Value", Value!);
    }

    class IncludingDocument(IFragment fragment) : IDocument
    {
        public void Inbound(IInboundContext context) => context.IncludeFragment(fragment);
    }

    class RegisteredDocument : IDocument
    {
        public void Inbound(IInboundContext context) => context.IncludeFragment("default-value-fragment");
    }

    class RepeatedDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.IncludeFragment(new ValueFragment { Value = "first" });
            context.IncludeFragment(new ValueFragment());
        }
    }

    class InitializedFragment : IFragment
    {
        [FragmentVariable(DefaultValue = "fallback")]
        public string Value { get; set; } = "initial";

        public void Fragment(IFragmentContext context) => context.SetHeader("X-Value", Value);
    }

    [TestMethod]
    [DataRow(null, "fallback")]
    [DataRow("", "")]
    [DataRow("explicit", "explicit")]
    public void ShouldDefaultOnlyNullFragmentVariables(string? value, string expected)
    {
        var test = new IncludingDocument(new ValueFragment { Value = value }).AsTestDocument();

        test.RunInbound();

        test.Context.Request.Headers["X-Value"].Should().Equal(expected);
    }

    [TestMethod]
    public void ShouldPreservePropertyInitializers()
    {
        var test = new IncludingDocument(new InitializedFragment()).AsTestDocument();

        test.RunInbound();

        test.Context.Request.Headers["X-Value"].Should().Equal("initial");
    }

    [TestMethod]
    public void ShouldUseDefaultsForIndependentFragmentInstances()
    {
        var test = new RepeatedDocument().AsTestDocument();

        test.RunInbound();

        test.Context.Request.Headers["X-Value"].Should().Equal("fallback");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldDefaultNamedFragments(bool registered)
    {
        var test = new RegisteredDocument().AsTestDocument();
        if (registered)
            test.RegisterFragment("default-value-fragment", new ValueFragment());

        test.RunInbound();

        test.Context.Request.Headers["X-Value"].Should().Equal("fallback");
    }
}
