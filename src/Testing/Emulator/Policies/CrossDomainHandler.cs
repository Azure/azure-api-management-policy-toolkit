// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class CrossDomainHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, string, bool>,
        Action<GatewayContext, string>
    >> CallbackHooks { get; } = new();

    public string PolicyName => nameof(IInboundContext.CrossDomain);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var policy = args.ExtractArgument<string>();
        ValidatePolicy(policy);

        var callbackHook = CallbackHooks.Find(hook => hook.Item1(context, policy));
        if (callbackHook is not null)
        {
            callbackHook.Item2(context, policy);
            return null;
        }

        // CrossDomain is a config/document policy in APIM; the emulator intentionally does not
        // simulate Adobe route serving or XML execution. Default execution remains a no-op after
        // validation, and callbacks receive the original raw XML value unchanged.
        return null;
    }

    private static void ValidatePolicy(string policy)
    {
        if (string.IsNullOrWhiteSpace(policy))
        {
            throw new ArgumentException("CrossDomain requires a non-empty XML policy document.", nameof(policy));
        }

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using var stringReader = new StringReader(policy);
            using var xmlReader = XmlReader.Create(stringReader, settings);
            var document = XDocument.Load(xmlReader);

            if (document.Root is null)
            {
                throw new ArgumentException("CrossDomain XML must contain a root element.", nameof(policy));
            }

            if (!string.IsNullOrEmpty(document.Root.Name.NamespaceName))
            {
                throw new ArgumentException("CrossDomain XML must use the unqualified <cross-domain-policy> root element.", nameof(policy));
            }

            if (!string.Equals(document.Root.Name.LocalName, "cross-domain-policy", StringComparison.Ordinal))
            {
                throw new ArgumentException("CrossDomain XML must use the <cross-domain-policy> root element.", nameof(policy));
            }
        }
        catch (XmlException ex)
        {
            throw new ArgumentException("CrossDomain XML must be well-formed.", nameof(policy), ex);
        }
    }
}
