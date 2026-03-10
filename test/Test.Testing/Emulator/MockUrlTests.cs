// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Test.Emulator.Emulator;

[TestClass]
public class MockUrlTests
{
    [TestMethod]
    public void MockUrl_ToString_DefaultValues()
    {
        // Arrange
        var url = new MockUrl();

        // Act
        var result = url.ToString();

        // Assert
        result.Should().Be("https://contoso.example/v2/mock/op");
    }

    [TestMethod]
    public void MockUrl_ToString_WithNonDefaultPort()
    {
        // Arrange
        var url = new MockUrl { Port = "8443" };

        // Act
        var result = url.ToString();

        // Assert
        result.Should().Be("https://contoso.example:8443/v2/mock/op");
    }

    [TestMethod]
    public void MockUrl_ToString_WithQueryString()
    {
        // Arrange
        var url = new MockUrl
        {
            Query = new Dictionary<string, string[]>
            {
                { "key", new[] { "value" } },
            },
        };

        // Act
        var result = url.ToString();

        // Assert
        result.Should().Be("https://contoso.example/v2/mock/op?key=value");
    }

    [TestMethod]
    public void MockUrl_ToString_HttpDefaultPort()
    {
        // Arrange
        var url = new MockUrl { Scheme = "http", Port = "80" };

        // Act
        var result = url.ToString();

        // Assert
        result.Should().Be("http://contoso.example/v2/mock/op");
    }
}
