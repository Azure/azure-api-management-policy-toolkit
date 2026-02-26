// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

internal class SectionContextProxy<TSection> : DispatchProxy where TSection : class
{
    private GatewayContext _context = null!;

    private Dictionary<string, IPolicyHandler> _handlers = null!;

    private readonly string _sectionName = typeof(TSection).Name;

    public TSection Object => this as TSection ?? throw new InvalidOperationException();

    public static SectionContextProxy<TSection> Create(GatewayContext expressionContext)
    {
        var context =
            (Create(typeof(TSection), typeof(SectionContextProxy<TSection>)) as SectionContextProxy<TSection>)!;
        context._context = expressionContext;
        context._handlers = DiscoverHandlers<TSection>();
        return context;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        ArgumentNullException.ThrowIfNull(targetMethod.DeclaringType);

        if (!_handlers.TryGetValue(targetMethod.Name, out var handler))
        {
            throw new NotImplementedException(targetMethod.Name);
        }

        try
        {
            return handler.Handle(_context, args);
        }
        catch (FinishSectionProcessingException) { throw; }
        catch (PolicyException) { throw; }
        catch (BadRuntimeConfigurationException) { throw; }
        catch (Exception e)
        {
            throw new PolicyException(e) { Policy = targetMethod.Name, Section = _sectionName, PolicyArgs = args };
        }
    }

    internal THandler GetHandler<THandler>() where THandler : class, IPolicyHandler
    {
        var scopes = typeof(THandler).GetCustomAttributes<SectionAttribute>().Select(att => att.Scope).ToArray();
        if (!scopes.Contains(_sectionName))
        {
            throw new ArgumentException(
                $"Handler define {string.Join(',', scopes)} but is tried to be fetched form {_sectionName} scope");
        }

        var tHandler = Activator.CreateInstance<THandler>();
        if (_handlers.TryGetValue(tHandler.PolicyName, out var handler))
        {
            return handler as THandler ?? throw new InvalidOperationException(
                $"Handler of type {typeof(THandler).Name} was requested but handler table contains one of type {handler.GetType().Name}");
        }

        _handlers[tHandler.PolicyName] = tHandler;
        return tHandler;
    }

    private static Dictionary<string, IPolicyHandler> DiscoverHandlers<T>()
    {
        var targetScope = typeof(T).Name;
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type =>
                type is
                {
                    IsClass: true,
                    IsAbstract: false,
                    Namespace: "Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies"
                }
                && typeof(IPolicyHandler).IsAssignableFrom(type)
                && type.GetCustomAttributes<SectionAttribute>().Any(att => att.Scope == targetScope))
            .Select(t => Activator.CreateInstance(t) as IPolicyHandler)
            .Where(h => h is not null)
            .ToDictionary(h => h!.PolicyName)!;
    }
}