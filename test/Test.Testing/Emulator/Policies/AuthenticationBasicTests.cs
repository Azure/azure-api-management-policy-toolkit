// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

using System.Text;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class AuthenticationBasicTests
{
    class BasicAuthDocument : IDocument
    {
        private readonly string _username;
        private readonly string _password;

        public BasicAuthDocument(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public void Inbound(IInboundContext context)
        {
            context.AuthenticationBasic(_username, _password);
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
    public void AuthenticationBasic_HandleSimple_UsesUtf8BasicHeaderValue()
    {
        // Arrange
        var test = new BasicAuthDocument("test", "tset").AsTestDocument();

        // Act
        test.RunInbound();

        // Assert
        var authHeader = test.Context.Request.Headers.GetValueOrDefault("Authorization");
        authHeader.Should().NotBeNullOrEmpty().And.StartWith("Basic ");
        authHeader.TryParseBasic(out var credentials).Should().BeTrue();
        credentials.Should().NotBeNull();
        credentials!.Username.Should().Be("test");
        credentials.Password.Should().Be("tset");

        authHeader.Should().Be($"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes("test:tset"))}");
    }

    [TestMethod]
    public void AuthenticationBasic_HandleUnicodeUtf8Credentials()
    {
        // Arrange
        const string username = "üser";
        const string password = "päss🙂";
        var expected = $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"))}";
        var test = new BasicAuthDocument(username, password).AsTestDocument();

        // Act
        test.RunInbound();

        // Assert
        var authHeader = test.Context.Request.Headers.GetValueOrDefault("Authorization");
        authHeader.Should().Be(expected);
    }

    [TestMethod]
    public void AuthenticationBasic_HandleEmptyCredentials_UsesColonValue()
    {
        // Arrange
        var test = new BasicAuthDocument(string.Empty, string.Empty).AsTestDocument();

        // Act
        test.RunInbound();

        // Assert
        var authHeader = test.Context.Request.Headers.GetValueOrDefault("Authorization");
        authHeader.Should().Be("Basic Og==");
    }

    [TestMethod]
    public void AuthenticationBasic_OverwriteExistingAuthorizationHeader()
    {
        // Arrange
        var test = new BasicAuthDocument("test", "tset").AsTestDocument();
        test.Context.Request.Headers["Authorization"] = ["Basic old:header"];

        // Act
        test.RunInbound();

        // Assert
        var authHeader = test.Context.Request.Headers.GetValueOrDefault("Authorization");
        authHeader.Should().Be($"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes("test:tset"))}");
    }

    [TestMethod]
    public void AuthenticationBasic_HandleCallback_UsesExactHeaderValue()
    {
        // Arrange
        var expected = "callback-user:callback-pass";
        var test = new BasicAuthDocument("test", "tset").AsTestDocument();
        test.SetupInbound().AuthenticationBasic().WithCallback((context, user, pass) =>
        {
            context.Request.Headers["Authorization"] = [$"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(expected))}"];
        });

        // Act
        test.RunInbound();

        // Assert
        var authHeader = test.Context.Request.Headers.GetValueOrDefault("Authorization");
        authHeader.Should().Be($"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(expected))}");
        authHeader.TryParseBasic(out var credentials).Should().BeTrue();
        credentials.Should().NotBeNull();
        credentials!.Username.Should().Be("callback-user");
        credentials.Password.Should().Be("callback-pass");
        authHeader.Should().NotBe($"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes("test:tset"))}");
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