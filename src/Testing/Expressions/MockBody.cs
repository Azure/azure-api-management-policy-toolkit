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
        if (Content == null) throw new NullReferenceException();

        Consumed = !preserveContent;

        if (typeof(T) == typeof(byte[])) return (T)(object)Encoding.UTF8.GetBytes(Content);
        if (typeof(T) == typeof(string)) return (T)(object)Content;
        if (typeof(T) == typeof(JObject)) return (T)(object)JObject.Parse(Content);
        if (typeof(T) == typeof(JToken)) return (T)(object)JToken.Parse(Content);
        if (typeof(T) == typeof(XNode))
        {
            using var reader = new XmlTextReader(Content);
            return (T)(object)XNode.ReadFrom(reader);
        }

        if (typeof(T) == typeof(XElement)) return (T)(object)XElement.Parse(Content);
        if (typeof(T) == typeof(XDocument)) return (T)(object)XDocument.Parse(Content);

        throw new NotImplementedException();
    }

    public IDictionary<string, IList<string>> AsFormUrlEncodedContent(bool preserveContent = false)
    {
        throw new NotImplementedException();
    }
}