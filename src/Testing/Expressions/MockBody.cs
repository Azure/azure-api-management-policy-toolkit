// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Xml;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockBody : IMessageBody
{
    public string? Content { get; set; }

    public bool Consumed { get; private set; } = false;

    public T As<T>(bool preserveContent = false)
    {
        var content = Content ?? string.Empty;

        Consumed = !preserveContent;

        if (typeof(T) == typeof(byte[])) return (T)(object)Encoding.UTF8.GetBytes(content);
        if (typeof(T) == typeof(string)) return (T)(object)content;
        if (typeof(T) == typeof(JObject)) return (T)(object)JObject.Parse(content);
        if (typeof(T) == typeof(JArray)) return (T)(object)JArray.Parse(content);
        if (typeof(T) == typeof(JToken)) return (T)(object)JToken.Parse(content);
        if (typeof(T) == typeof(XNode))
        {
            using var reader = new XmlTextReader(content);
            return (T)(object)XNode.ReadFrom(reader);
        }

        if (typeof(T) == typeof(XElement)) return (T)(object)XElement.Parse(content);
        if (typeof(T) == typeof(XDocument)) return (T)(object)XDocument.Parse(content);

        throw new NotImplementedException();
    }

    public IDictionary<string, IList<string>> AsFormUrlEncodedContent(bool preserveContent = false)
    {
        throw new NotImplementedException();
    }
}
