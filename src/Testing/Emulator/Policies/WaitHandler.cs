// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class WaitHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, Action, string?, bool>,
        Action<GatewayContext, Action, string?>
    >> CallbackHooks { get; } = [];

    public string PolicyName => nameof(IInboundContext.Wait);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        if (args is not { Length: 2 })
        {
            throw new ArgumentException("Expected 2 arguments.", nameof(args));
        }
        ArgumentNullException.ThrowIfNull(args[0], "section");
        var (section, waitFor) = args.ExtractArguments<Action, string>();
        if (waitFor is not (null or "all" or "any"))
        {
            throw new ArgumentException("Wait for must be 'all' or 'any'.", nameof(waitFor));
        }

        if (RetryHandler.IsExecuting(context))
        {
            throw new InvalidOperationException("The wait policy cannot be nested inside retry.");
        }

        var callback = CallbackHooks.Find(hook => hook.Item1(context, section, waitFor));
        if (callback is not null)
        {
            callback.Item2(context, section, waitFor);
            return null;
        }

        throw new NotSupportedException(
            "Parallel wait requires immediate child capture, gateway context isolation, and child cancellation " +
            "in SectionContextProxy. The accepted execution proxy does not provide these capabilities. " +
            "Use SetupInbound().Wait().WithCallback(...) (or the appropriate section) only for an explicit mock.");
    }
}
