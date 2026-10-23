// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

[TestClass]
public class MockBodyTests
{
    [TestMethod]
    public void As_ShouldAllowRepeatedReadsWhenContentIsPreserved()
    {
        // Arrange
        var body = new MockBody { Content = "payload" };

        // Act
        var first = body.As<string>(preserveContent: true);
        var second = body.As<string>(preserveContent: true);

        // Assert
        first.Should().Be("payload");
        second.Should().Be("payload");
        body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    public void As_ShouldReadXNode()
    {
        // Arrange
        var body = new MockBody { Content = "<a>1</a>" };

        // Act
        var node = body.As<XNode>(preserveContent: true);

        // Assert
        node.Should().BeOfType<XElement>().Which.Value.Should().Be("1");
    }

    [TestMethod]
    public void As_ShouldThrowWhenBodyWasConsumed()
    {
        // Arrange
        var body = new MockBody { Content = "payload" };

        // Act
        var first = body.As<string>();
        var secondRead = () => body.As<string>(preserveContent: true);

        // Assert
        first.Should().Be("payload");
        body.Consumed.Should().BeTrue();
        secondRead.Should().Throw<NullReferenceException>()
            .WithMessage("Object reference not set to an instance of an object.");
    }

    [TestMethod]
    public void AsFormUrlEncodedContent_ShouldThrowWhenBodyWasConsumed()
    {
        // Arrange
        var body = new MockBody { Content = "a=1" };
        body.As<string>();

        // Act
        var read = () => body.AsFormUrlEncodedContent();

        // Assert
        read.Should().Throw<NullReferenceException>();
    }

    [TestMethod]
    public void AsFormUrlEncodedContent_ShouldDecodeFormContent()
    {
        // Arrange
        var body = new MockBody { Content = "a=1&a=2&b=x+y&c=%26%3D&d" };

        // Act
        var result = body.AsFormUrlEncodedContent(preserveContent: true);

        // Assert
        result.Should().HaveCount(4);
        result["a"].Should().Equal("1", "2");
        result["b"].Should().Equal("x y");
        result["c"].Should().Equal("&=");
        result["d"].Should().Equal("");
        body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    public void AsFormUrlEncodedContent_ShouldConsumeBody()
    {
        // Arrange
        var body = new MockBody { Content = "a=1" };

        // Act
        body.AsFormUrlEncodedContent();

        // Assert
        body.Consumed.Should().BeTrue();
    }

    [TestMethod]
    public void Content_ShouldMakeConsumedBodyReadableAgain()
    {
        // Arrange
        var body = new MockBody { Content = "payload" };
        body.As<string>();

        // Act
        body.Content = "new-payload";

        // Assert
        body.Consumed.Should().BeFalse();
        body.As<string>().Should().Be("new-payload");
    }
}
