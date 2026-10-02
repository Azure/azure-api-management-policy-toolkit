// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

using Newtonsoft.Json;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

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
        ValidateConfig(config);
        var headers = context.Request.Headers
            .Where(header => string.Equals(header.Key, config.Name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var comparer = ValueComparer(config);
        var pass = headers.Length > 0 && (config.Values.Length == 0
            || headers.SelectMany(header => header.Value).Any(value => config.Values.Contains(value, comparer)));

        if (pass)
        {
            OnCheckPassed.Find(tuple => tuple.Item1(context, config))?.Item2(context, config);
            return;
        }

        var body = JsonConvert.SerializeObject(new
        {
            statusCode = config.FailCheckHttpCode,
            message = config.FailCheckErrorMessage
        });
        ResponseUtilities.Overwrite(context.Response, config.FailCheckHttpCode);
        context.Response.Headers["Content-Type"] = ["application/json"];
        context.Response.Body.Content = body;

        OnCheckFailed.Find(tuple => tuple.Item1(context, config))?.Item2(context, config);
        throw new FinishSectionProcessingException();
    }

    private static StringComparer ValueComparer(CheckHeaderConfig config) => config.IgnoreCase
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static void ValidateConfig(CheckHeaderConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Name);
        if (!config.Name.All(character =>
                char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-.^_`|~".Contains(character)))
        {
            throw new ArgumentException("The header name must be an HTTP token.", nameof(config.Name));
        }

        ArgumentNullException.ThrowIfNull(config.Values);
        foreach (var value in config.Values)
        {
            ArgumentNullException.ThrowIfNull(value);
        }

        ArgumentNullException.ThrowIfNull(config.FailCheckErrorMessage);
        if (config.FailCheckHttpCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(config.FailCheckHttpCode), config.FailCheckHttpCode,
                "HTTP response status codes must be between 100 and 599.");
        }
    }
}
