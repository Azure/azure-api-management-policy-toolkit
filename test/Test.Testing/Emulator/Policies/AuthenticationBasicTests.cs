// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class AuthenticationBasicTests
{
    class SimpleABasic : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AuthenticationBasic("test", "tset");
        }
    }

    class MultipleABasic : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AuthenticationBasic("a", "a-pass");
            context.AuthenticationBasic("b", "b-pass");
        }
    }

    [TestMethod]
    public void AuthenticationBasic_HandleSimple()
    {
        // Arrange
        var test = new SimpleABasic().AsTestDocument();

        // Act
        test.RunInbound();

        // Assert
        var authHeader = test.Context.Request.Headers.GetValueOrDefault("Authorization");
        authHeader.Should().NotBeNullOrEmpty().And.StartWith("Basic ");
        authHeader.TryParseBasic(out var credentials).Should().BeTrue();
        credentials.Should().NotBeNull();
        credentials!.Username.Should().Be("test");
        credentials.Password.Should().Be("tset");
    }

    [TestMethod]
    public void AuthenticationBasic_HandleCallback()
    {
        // Arrange
        var test = new SimpleABasic().AsTestDocument();
        test.SetupInbound().AuthenticationBasic().WithCallback((context, user, pass) =>
        {
            context.Request.Headers["Authorization"] = [$"Basic {user}:{pass}"];
        });

        // Act
        test.RunInbound();

        // Assert
        var authHeader = test.Context.Request.Headers.GetValueOrDefault("Authorization");
        authHeader.Should().NotBeNullOrEmpty().And.StartWith("Basic ").And.EndWith("test:tset");
    }

    [TestMethod]
    public void AuthenticationBasic_HandleCallback_WithPredicate()
    {
        // Arrange
        var test = new MultipleABasic().AsTestDocument();
        test.SetupInbound()
            .AuthenticationBasic((_, user, pass) => user == "b" && pass == "b-pass")
            .WithCallback((context, user, pass) =>
            {
                context.Request.Headers["B"] = [$"{user}:{pass}"];
            });
        test.SetupInbound()
            .AuthenticationBasic((_, user, pass) => user == "a" && pass == "a-pass")
            .WithCallback((context, user, pass) =>
            {
                context.Request.Headers["A"] = [$"{user}:{pass}"];
            });

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request
            .Headers.Should().ContainKey("A")
            .WhoseValue.Should().ContainSingle()
            .Which.Should().Be("a:a-pass");
        test.Context.Request
            .Headers.Should().ContainKey("B")
            .WhoseValue.Should().ContainSingle()
            .Which.Should().Be("b:b-pass");
    }
}