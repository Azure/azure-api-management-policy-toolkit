// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class AppendQueryParameterTests
{
    class SimpleAppendQueryParameter : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AppendQueryParameter("param1", "value-1", "value-2");
        }
    }

    class MultipleAppendQueryParameter : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AppendQueryParameter("paramA", "A");
            context.AppendQueryParameter("paramB", "B");
        }
    }

    [TestMethod]
    public void AppendQueryParameter_ShouldCreateParam_WhenNotExist()
    {
        // Arrange
        var test = new SimpleAppendQueryParameter().AsTestDocument();

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Url
            .Query.Should().ContainKey("param1")
            .WhoseValue.Should().ContainInOrder("value-1", "value-2");
    }

    [TestMethod]
    public void AppendQueryParameter_ShouldAppendParams_WhenExist()
    {
        // Arrange
        var test = new TestDocument(new SimpleAppendQueryParameter())
        {
            Context = { Request = { Url = { Query = { { "param1", ["value-0"] } } } } }
        };

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Url
            .Query.Should().ContainKey("param1")
            .WhoseValue.Should().ContainInOrder("value-0", "value-1", "value-2");
    }

    [TestMethod]
    public void AppendQueryParameter_WithCallback()
    {
        // Arrange
        var test = new SimpleAppendQueryParameter().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().AppendQueryParameter().WithCallback(((context, name, values) =>
        {
            callbackExecuted = true;
            context.Request.Url.Query.Add(name, values.Reverse().ToArray());
        }));

        // Act
        test.RunInbound();

        // Assert
        callbackExecuted.Should().BeTrue();
        test.Context.Request.Url
            .Query.Should().ContainKey("param1")
            .WhoseValue.Should().ContainInOrder("value-2", "value-1");
    }

    [TestMethod]
    public void AppendQueryParameter_WithPredicateCallback()
    {
        // Arrange
        var test = new TestDocument(new MultipleAppendQueryParameter())
        {
            Context = { Request = { Url = { Query = { { "paramA", ["AA"] } } } } }
        };
        var callbackExecuted = false;
        test.SetupInbound().AppendQueryParameter((_, name, _) => name == "paramB").WithCallback(((_, _, _) =>
        {
            callbackExecuted = true;
        }));

        // Act
        test.RunInbound();

        // Assert
        callbackExecuted.Should().BeTrue();
        var query = test.Context.Request.Url.Query;
        query.Should().ContainKey("paramA")
            .WhoseValue.Should().ContainInOrder("AA", "A");
        query.Should().NotContainKey("paramB");
    }
}