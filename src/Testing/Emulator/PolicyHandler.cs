using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

internal abstract class PolicyHandler<TConfig> : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, TConfig, bool>,
        Action<GatewayContext, TConfig>
    >> CallbackSetup { get; } = new();

    public abstract string PolicyName { get; }

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var config = args.ExtractArgument<TConfig>();
        var callbackHook = CallbackSetup.Find(hook => hook.Item1(context, config));
        if (callbackHook is not null)
        {
            callbackHook.Item2(context, config);
        }
        else
        {
            Handle(context, config);
        }

        return null;
    }

    protected abstract void Handle(GatewayContext context, TConfig config);
}

internal abstract class PolicyHandlerOptionalParam<TConfig> : IPolicyHandler
    where TConfig : class
{
    public List<Tuple<
        Func<GatewayContext, TConfig?, bool>,
        Action<GatewayContext, TConfig?>
    >> CallbackSetup { get; } = new();

    public abstract string PolicyName { get; }

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var config = args.ExtractOptionalArgument<TConfig>();
        var callbackHook = CallbackSetup.Find(hook => hook.Item1(context, config));
        if (callbackHook is not null)
        {
            callbackHook.Item2(context, config);
        }
        else
        {
            Handle(context, config);
        }

        return null;
    }

    protected abstract void Handle(GatewayContext context, TConfig? config);
}

internal abstract class PolicyHandler<TParam1, TParam2> : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, TParam1, TParam2, bool>,
        Action<GatewayContext, TParam1, TParam2>
    >> CallbackSetup { get; } = new();

    public abstract string PolicyName { get; }

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var (param1, param2) = args.ExtractArguments<TParam1, TParam2>();
        var callbackHook = CallbackSetup.Find(hook => hook.Item1(context, param1, param2));
        if (callbackHook is not null)
        {
            callbackHook.Item2(context, param1, param2);
        }
        else
        {
            Handle(context, param1, param2);
        }

        return null;
    }

    protected abstract void Handle(GatewayContext context, TParam1 param1, TParam2 param2);
}