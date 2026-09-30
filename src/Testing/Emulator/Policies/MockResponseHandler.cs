// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class MockResponseHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, MockResponseConfig?, bool>,
        Action<GatewayContext, MockResponseConfig?>
    >> CallbackSetup { get; } = new();

    public string PolicyName => nameof(IInboundContext.MockResponse);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var config = args.ExtractOptionalArgument<MockResponseConfig>();
        try
        {
            var callback = CallbackSetup.Find(hook => hook.Item1(context, config));
            if (callback is not null)
            {
                callback.Item2(context, config);
            }
            else
            {
                Handle(context, config);
            }
        }
        catch (FinishSectionProcessingException termination)
        {
            termination.TerminatesPipeline = true;
            context.RecordTermination(termination);
            throw;
        }

        context.ResponseTerminated = true;
        throw new FinishSectionProcessingException();
    }

    private static void Handle(GatewayContext context, MockResponseConfig? config)
    {
        config ??= new MockResponseConfig();
        var statusCode = config.StatusCode ?? 200;
        ValidateStatusCode(statusCode, nameof(config.StatusCode));
        var examples = context.ResponseExampleStore.GetOrDefault(context.Api.Id, context.Operation.Id);
        var example = ChooseExample(config, statusCode, examples);
        statusCode = example?.ResponseCode ?? statusCode;
        ValidateStatusCode(statusCode, nameof(ResponseExample.ResponseCode));

        using var message = new HttpResponseMessage((HttpStatusCode)statusCode);
        var response = context.Response;
        ResponseUtilities.Overwrite(response, statusCode, message.ReasonPhrase ?? string.Empty);

        var contentType = example?.ContentType ?? config.ContentType;
        if (contentType is not null)
        {
            response.Headers["Content-Type"] = [contentType];
        }

        var sample = example?.Sample ?? string.Empty;
        response.Body.Content = sample;
        response.Headers["Content-Length"] =
            [Encoding.UTF8.GetByteCount(sample).ToString(CultureInfo.InvariantCulture)];
    }

    private static ResponseExample? ChooseExample(
        MockResponseConfig config, int statusCode, ResponseExample[] examples)
    {
        if (config.Index is { } index)
        {
            if (index < 0 || index >= examples.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(config.Index), index,
                    "The sample index must be nonnegative and refer to an available response example.");
            }

            return examples[index];
        }

        return examples.FirstOrDefault(example =>
            example.ResponseCode == statusCode &&
            (config.ContentType is null ||
             string.Equals(example.ContentType, config.ContentType, StringComparison.OrdinalIgnoreCase)));
    }

    private static void ValidateStatusCode(int statusCode, string parameterName)
    {
        if (statusCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(parameterName, statusCode,
                "HTTP response status codes must be between 100 and 599.");
        }
    }
}