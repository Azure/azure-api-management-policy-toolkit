// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class MockResponseHandler : PolicyHandlerOptionalParam<MockResponseConfig>
{
    public override string PolicyName => nameof(IInboundContext.MockResponse);

    protected override void Handle(GatewayContext context, MockResponseConfig? config)
    {
        if (config?.StatusCode is null)
        {
            config = new MockResponseConfig
            {
                StatusCode = 200, ContentType = config?.ContentType, Index = config?.Index
            };
        }

        var examples = context.ResponseExampleStore.GetOrDefault(context.Api.Id, context.Operation.Id);
        var example = ChooseExample(config, examples);

        var response = context.Response;
        response.StatusCode = example?.ResponseCode ?? config.StatusCode ?? 200;
        response.StatusReason = string.Empty;
        response.Headers.Clear();

        if (example?.ContentType is not null)
        {
            response.Headers["Content-Type"] = [example.ContentType];
        }

        if (example?.Sample is not null)
        {
            response.Body.Content = example.Sample;
            response.Headers["Content-Length"] = [response.Body.Content.Length.ToString(CultureInfo.InvariantCulture)];
        }
        else
        {
            response.Headers["Content-Length"] = ["0"];
        }

        throw new FinishSectionProcessingException();
    }

    private static ResponseExample? ChooseExample(MockResponseConfig config, ResponseExample[] examples)
    {
        if (config.Index.HasValue && examples.Length > config.Index.Value)
        {
            return examples[config.Index.Value];
        }

        var example = examples.FirstOrDefault(e =>
            e.ResponseCode == config.StatusCode &&
            string.Equals(e.ContentType, config.ContentType, StringComparison.OrdinalIgnoreCase));
        return example ?? examples.FirstOrDefault(e => e.ResponseCode == config.StatusCode);
    }
}