// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions.Extensions;

[TestClass]
public class UrlContentEncoderTests
{
    [TestMethod]
    public void UrlContentEncoder_ShouldReturnNullForNullDictionary()
    {
        // Act
        var result = new UrlContentEncoder().Encode(null);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void UrlContentEncoder_ShouldReturnNullForEmptyDictionary()
    {
        // Act
        var result = new UrlContentEncoder().Encode(new Dictionary<string, IList<string>>());

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void UrlContentEncoder_ShouldReturnStringWithoutAmpersandSign()
    {
        // Arrange
        var dictionary = new Dictionary<string, IList<string>> { { "key1", new List<string> { "value1" } } };

        // Act
        var result = new UrlContentEncoder().Encode(dictionary);

        // Assert
        result.Should().NotBeNullOrWhiteSpace().And.NotContainAny("&").And.BeEquivalentTo("key1=value1");
    }

    [TestMethod]
    public void UrlContentEncoder_ShouldReturnStringWithAmpersandSignForMultipleKeys()
    {
        // Arrange
        var dictionary = new Dictionary<string, IList<string>>
        {
            { "key1", new List<string> { "1-value1" } }, { "key2", new List<string> { "2-value1" } }
        };

        // Act
        var result = new UrlContentEncoder().Encode(dictionary);

        // Assert
        result.Should().NotBeNullOrWhiteSpace().And.BeEquivalentTo("key1=1-value1&key2=2-value1");
    }

    [TestMethod]
    public void UrlContentEncoder_ShouldReturnStringWithAmpersandSignForValues()
    {
        // Arrange
        var dictionary = new Dictionary<string, IList<string>>
        {
            { "key1", new List<string> { "1-value1", "1-value2" } }
        };

        // Act
        var result = new UrlContentEncoder().Encode(dictionary);

        // Assert
        result.Should().NotBeNullOrWhiteSpace().And.BeEquivalentTo("key1=1-value1&key1=1-value2");
    }

    [TestMethod]
    public void UrlContentEncoder_ShouldHtmlEncodeKey()
    {
        // Arrange
        var dictionary = new Dictionary<string, IList<string>>
        {
            { "!@#$%^&*()-=+,.:;<>?/\"\\|[]{}`~", new List<string> { "value" } }
        };

        // Act
        var result = new UrlContentEncoder().Encode(dictionary);

        // Assert
        result.Should().NotBeNullOrWhiteSpace().And
            .BeEquivalentTo("!@%23$%25%5E%26*()-%3D%2B,.%3A;%3C%3E%3F%2F%22%5C%7C%5B%5D%7B%7D%60~=value");
    }

    [TestMethod]
    public void UrlContentEncoder_ShouldHtmlEncodeValue()
    {
        // Arrange
        var dictionary = new Dictionary<string, IList<string>>
        {
            { "key", new List<string> { "!@#$%^&*()-=+,.:;<>?/\"\\|[]{}`~" } }
        };

        // Act
        var result = new UrlContentEncoder().Encode(dictionary);

        // Assert
        result.Should().NotBeNullOrWhiteSpace().And
            .BeEquivalentTo("key=!@%23$%25%5E%26*()-%3D%2B,.%3A;%3C%3E%3F%2F%22%5C%7C%5B%5D%7B%7D%60~");
    }
}