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
    private string? _content;

    // Setting new content (for example by set-body) makes the body readable again.
    public string? Content
    {
        get => _content;
        set
        {
            _content = value;
            Consumed = false;
        }
    }

    public bool Consumed { get; private set; } = false;

    public T As<T>(bool preserveContent = false)
    {
        ThrowIfConsumed();
        var content = Content ?? string.Empty;

        Consumed = !preserveContent;

        if (typeof(T) == typeof(byte[])) return (T)(object)Encoding.UTF8.GetBytes(content);
        if (typeof(T) == typeof(string)) return (T)(object)content;
        if (typeof(T) == typeof(JObject)) return (T)(object)JObject.Parse(content);
        if (typeof(T) == typeof(JArray)) return (T)(object)JArray.Parse(content);
        if (typeof(T) == typeof(JToken)) return (T)(object)JToken.Parse(content);
        if (typeof(T) == typeof(XNode))
        {
            using var reader = XmlReader.Create(new StringReader(content));
            reader.MoveToContent();
            return (T)(object)XNode.ReadFrom(reader);
        }

        if (typeof(T) == typeof(XElement)) return (T)(object)XElement.Parse(content);
        if (typeof(T) == typeof(XDocument)) return (T)(object)XDocument.Parse(content);

        throw new NotImplementedException();
    }

    public IDictionary<string, IList<string>> AsFormUrlEncodedContent(bool preserveContent = false)
    {
        ThrowIfConsumed();
        var content = Content ?? string.Empty;

        Consumed = !preserveContent;

        var result = new Dictionary<string, IList<string>>();
        foreach (var pair in content.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Decode(parts[0]);
            var value = parts.Length > 1 ? Decode(parts[1]) : string.Empty;
            if (!result.TryGetValue(key, out var values))
            {
                values = new List<string>();
                result[key] = values;
            }

            values.Add(value);
        }

        return result;
    }

    // API Management makes the body unavailable after a read without preserveContent; reading it again throws a
    // NullReferenceException ("Object reference not set to an instance of an object."), so the emulator does too.
    // Read it with preserveContent: true to access it again.
    private void ThrowIfConsumed()
    {
        if (Consumed)
        {
            throw new NullReferenceException();
        }
    }

    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
}
