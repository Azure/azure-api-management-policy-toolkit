// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Formatting = Newtonsoft.Json.Formatting;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies
{
    [
        Section(nameof(IInboundContext)),
        Section(nameof(IBackendContext)),
        Section(nameof(IOutboundContext)),
        Section(nameof(IOnErrorContext))
    ]
    internal class XmlToJsonHandler : PolicyHandler<XmlToJsonConfig>
    {
        public override string PolicyName => nameof(IInboundContext.XmlToJson);

        protected override void Handle(GatewayContext context, XmlToJsonConfig config)
        {
            if (config.Kind is not ("direct" or "javascript-friendly"))
            {
                throw new ArgumentException("Kind must be 'direct' or 'javascript-friendly'.", nameof(config));
            }

            BodyConversionUtilities.ValidateApply(config.Apply, "xml");
            var message = BodyConversionUtilities.Message(context, this);
            if (!BodyConversionUtilities.ShouldApply(
                context, message, config.Apply, config.ConsiderAcceptHeader, "xml", "json"))
            {
                return;
            }

            using var input = new StringReader(BodyConversionUtilities.Body(message, PolicyName));
            using var reader = XmlReader.Create(input, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreWhitespace = true,
            });
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            using var jsonInput = new StringReader(JsonConvert.SerializeXNode(document, Formatting.None));
            using var jsonReader = new JsonTextReader(jsonInput) { DateParseHandling = DateParseHandling.None };
            var json = JToken.ReadFrom(jsonReader);

            if (config.AlwaysArrayChildElements == true)
            {
                ForceChildArrays(json, false);
            }

            if (config.Kind == "javascript-friendly")
            {
                json = JavascriptFriendly(json);
            }

            BodyConversionUtilities.ReplaceBody(message, json.ToString(Formatting.None), "application/json");
        }

        private static void ForceChildArrays(JToken token, bool isElement)
        {
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties().ToArray())
                {
                    if (property.Name.StartsWith('@') || property.Name.StartsWith('#') || property.Name.StartsWith('?'))
                    {
                        continue;
                    }

                    ForceChildArrays(property.Value, true);
                    if (isElement && property.Value is not JArray)
                    {
                        property.Value = new JArray(property.Value.DeepClone());
                    }
                }
            }
            else if (token is JArray array)
            {
                foreach (var item in array)
                {
                    ForceChildArrays(item, true);
                }
            }
        }

        private static JToken JavascriptFriendly(JToken token)
        {
            if (token is JObject obj)
            {
                var result = new JObject();
                foreach (var property in obj.Properties())
                {
                    // APIM does not document the exact friendly wire shape. This projection omits declarations,
                    // flattens attributes, and uses '_' for prefixes; collisions fail instead of merging data.
                    if (property.Name.StartsWith('?') || property.Name == "@xmlns"
                        || property.Name.StartsWith("@xmlns:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var name = property.Name switch
                    {
                        "#text" => "$text",
                        "#cdata-section" => "$cdata",
                        "#comment" => "$comment",
                        "#whitespace" => "$whitespace",
                        "#significant-whitespace" => "$significantWhitespace",
                        _ => (property.Name.StartsWith('@') ? property.Name[1..] : property.Name).Replace(':', '_'),
                    };
                    if (result.ContainsKey(name))
                    {
                        throw new ArgumentException($"JavaScript-friendly conversion produces duplicate property '{name}'.");
                    }

                    result.Add(name, JavascriptFriendly(property.Value));
                }

                return result;
            }

            return token is JArray array
                ? new JArray(array.Select(JavascriptFriendly))
                : token.DeepClone();
        }
    }
}

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document
{
    /// <summary>
    /// Configures callbacks that override XML-to-JSON conversion in emulator policy sections.
    /// </summary>
    public static class MockXmlToJsonProvider
    {
        public static Setup XmlToJson<TSection>(this MockPoliciesProvider<TSection> mock) where TSection : class =>
            XmlToJson(mock, (_, _) => true);

        public static Setup XmlToJson<TSection>(
            this MockPoliciesProvider<TSection> mock,
            Func<GatewayContext, XmlToJsonConfig, bool> predicate) where TSection : class
        {
            ArgumentNullException.ThrowIfNull(predicate);
            return new Setup(predicate, mock.SectionContextProxy.GetHandler<XmlToJsonHandler>());
        }

        public class Setup
        {
            private readonly Func<GatewayContext, XmlToJsonConfig, bool> _predicate;
            private readonly XmlToJsonHandler _handler;

            internal Setup(Func<GatewayContext, XmlToJsonConfig, bool> predicate, XmlToJsonHandler handler)
            {
                _predicate = predicate;
                _handler = handler;
            }

            public void WithCallback(Action<GatewayContext, XmlToJsonConfig> callback)
            {
                ArgumentNullException.ThrowIfNull(callback);
                _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
            }
        }
    }
}
