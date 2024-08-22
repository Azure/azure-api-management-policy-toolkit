using System.Text;
using System.Xml;

using FluentAssertions.Xml;

using Mielek.Azure.ApiManagement.PolicyToolkit.Serialization;

namespace Mielek.Azure.ApiManagement.PolicyToolkit.Tests.Extensions;

public static class XElementAssertionsExtensions
{
    readonly static XmlWriterSettings DefaultSerializeSettings = new()
    {
        OmitXmlDeclaration = true,
        ConformanceLevel = ConformanceLevel.Fragment,
        Indent = true,
        IndentChars = "    "
    };

    //
    // Summary:
    //     Asserts that a string is exactly the same as policy xml serialized with indentation, without expression formatting
    public static void BeEquivalentTo(this XElementAssertions assertions, string expectedXml, string because = "", params object[] becauseArgs)
    {
        var strBuilder = new StringBuilder();
        using (var writer = CustomXmlWriter.Create(strBuilder, DefaultSerializeSettings))
        {
            writer.Write(assertions.Subject);
        }
        strBuilder.ToString().Should().BeEquivalentTo(expectedXml, because, becauseArgs);
    }
}