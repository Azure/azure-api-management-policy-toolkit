// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions.Extensions;

[TestClass]
public class BasicAuthCredentialsParserTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow(" ")]
    [DataRow("Digest value")]
    [DataRow("Basic ")]
    [DataRow("Basic notBase64Encoded")]
    public void BasicAuthCredentialsParser_ShouldReturnNull(string value)
    {
        // Act
        var result = new BasicAuthCredentialsParser().Parse(value);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void BasicAuthCredentialsParser_ShouldReturnFilledObject()
    {
        // Arrange
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("username:password"));
        var headerValue = $"Basic {credentials}";

        // Act
        var result = new BasicAuthCredentialsParser().Parse(headerValue);

        // Assert
        result.Should().NotBeNull();
        result!.Password.Should().Be("password");
        result!.Username.Should().Be("username");
    }

    [TestMethod]
    public void BasicAuthCredentialsParser_ShouldSplitOnLastDoubleDot()
    {
        // Arrange
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("user:name:password"));
        var headerValue = $"Basic {credentials}";

        // Act
        var result = new BasicAuthCredentialsParser().Parse(headerValue);

        // Assert
        result.Should().NotBeNull();
        result!.Password.Should().Be("password");
        result!.Username.Should().Be("user:name");
    }

    [TestMethod]
    public void BasicAuthCredentialsParser_ShouldHandleIso_8859_1Encoding()
    {
        // Arrange
        var credentials = Convert.ToBase64String(Encoding.GetEncoding("iso-8859-1").GetBytes("¡usernameÿ:password"));
        var headerValue = $"Basic {credentials}";

        // Act
        var result = new BasicAuthCredentialsParser().Parse(headerValue);

        // Assert
        result.Should().NotBeNull();
        result!.Password.Should().Be("password");
        result!.Username.Should().Be("¡usernameÿ");
    }
}