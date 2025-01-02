// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class CheckHeaderHandler : PolicyHandler<CheckHeaderConfig>
{
    public List<Tuple<
        Func<GatewayContext, CheckHeaderConfig, bool>,
        Action<GatewayContext, CheckHeaderConfig>
    >> OnCheckPassed { get; } = new();

    public List<Tuple<
        Func<GatewayContext, CheckHeaderConfig, bool>,
        Action<GatewayContext, CheckHeaderConfig>
    >> OnCheckFailed { get; } = new();

    public override string PolicyName => nameof(IInboundContext.CheckHeader);

    protected override void Handle(GatewayContext context, CheckHeaderConfig config)
    {
        bool pass = false;
        if (context.Request.Headers.TryGetValue(config.Name, out var values) && values.Length == 1)
        {
            pass = config.Values.Length == 0 || config.Values.Contains(values[0], ValueComparer(config));
        }

        if (pass)
        {
            OnCheckPassed.Find(tuple => tuple.Item1(context, config))?.Item2(context, config);
            return;
        }

        context.Response = new MockResponse()
        {
            StatusCode = config.FailCheckHttpCode,
            // StatusReason = GetStatusReason(config.FailCheckHttpCode), TODO: Create status reason mapper
            Headers = { { "Content-Type", ["application/json"] } },
            Body =
            {
                Content = $$"""
                            {
                              "statusCode": {{config.FailCheckHttpCode}},
                              "message": "{{config.FailCheckErrorMessage}}"
                            }
                            """
            }
        };

        OnCheckFailed.Find(tuple => tuple.Item1(context, config))?.Item2(context, config);
        throw new FinishSectionProcessingException();
    }

    private static StringComparer ValueComparer(CheckHeaderConfig config) => config.IgnoreCase
        ? StringComparer.InvariantCultureIgnoreCase
        : StringComparer.InvariantCulture;
}