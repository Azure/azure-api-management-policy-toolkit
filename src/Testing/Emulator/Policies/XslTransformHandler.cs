// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Xsl;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class XslTransformRequestHandler : XslTransformHandler
{
    protected override MockMessage GetMessage(GatewayContext context) => context.Request;
}

[Section(nameof(IOutboundContext)), Section(nameof(IOnErrorContext))]
internal class XslTransformResponseHandler : XslTransformHandler
{
    protected override MockMessage GetMessage(GatewayContext context) => context.Response;
}

internal abstract class XslTransformHandler : PolicyHandler<XslTransformConfig>
{
    public override string PolicyName => nameof(IInboundContext.XslTransform);

    protected override void Handle(GatewayContext context, XslTransformConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.StyleSheet, nameof(config.StyleSheet));

        var parameters = new XsltArgumentList();
        if (config.Parameters is not null)
        {
            foreach (var parameter in config.Parameters)
            {
                ArgumentNullException.ThrowIfNull(parameter);
                ArgumentException.ThrowIfNullOrWhiteSpace(parameter.Name, nameof(parameter.Name));
                ArgumentNullException.ThrowIfNull(parameter.Value, nameof(parameter.Value));
                parameters.AddParam(parameter.Name, string.Empty, parameter.Value);
            }
        }

        var readerSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        using var stylesheetText = new StringReader(config.StyleSheet);
        using var stylesheet = XmlReader.Create(stylesheetText, readerSettings);
        var transform = new XslCompiledTransform();
        transform.Load(stylesheet, new XsltSettings(enableDocumentFunction: false, enableScript: false), null);
        var outputSettings = transform.OutputSettings
            ?? throw new InvalidOperationException("XslTransform requires a compiled stylesheet.");
        if (outputSettings.Encoding.CodePage != Encoding.UTF8.CodePage)
        {
            throw new NotSupportedException(
                $"XslTransform supports only UTF-8 output because emulator message bodies are serialized as UTF-8. Requested encoding: '{outputSettings.Encoding.WebName}'.");
        }

        var message = GetMessage(context);
        var body = message.Body;
        var inputContent = body.Content;
        ArgumentException.ThrowIfNullOrWhiteSpace(inputContent, nameof(body.Content));
        using var inputText = new StringReader(inputContent);
        using var input = XmlReader.Create(inputText, readerSettings);
        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, outputSettings))
        {
            transform.Transform(input, parameters, writer, documentResolver: null);
        }

        var outputBytes = output.ToArray();
        var preamble = outputSettings.Encoding.GetPreamble();
        // The body stores text: remove the encoding preamble, not a literal leading BOM.
        var offset = outputBytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        var content = outputSettings.Encoding.GetString(outputBytes, offset, outputBytes.Length - offset);
        var contentLengthHeaders = message.Headers.Keys
            .Where(name => string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var contentLength = Encoding.UTF8.GetByteCount(content).ToString(CultureInfo.InvariantCulture);

        body.Content = content;
        foreach (var header in contentLengthHeaders)
        {
            message.Headers[header] = [contentLength];
        }
    }

    protected abstract MockMessage GetMessage(GatewayContext context);
}
