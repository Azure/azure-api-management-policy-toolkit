// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

public static class ArgumentsExtensions
{
    public static T ExtractArgument<T>(this object?[]? args)
    {
        if (args is not { Length: 1 })
        {
            throw new ArgumentException("Expected 1 argument", nameof(args));
        }

        if (args[0] is not T arg)
        {
            throw new ArgumentException($"Expected {typeof(T).Name} as first argument", nameof(args));
        }

        return arg;
    }

    public static T? ExtractOptionalArgument<T>(this object?[]? args) where T : class
    {
        if (args is null)
        {
            return null;
        }

        if (args is not { Length: 1 })
        {
            throw new ArgumentException("Expected only 1 argument", nameof(args));
        }

        if (args[0] is not null && args[0] is not T)
        {
            throw new ArgumentException($"Expected {typeof(T).Name} as first argument", nameof(args));
        }

        return args[0] as T;
    }

    public static (T1, T2) ExtractArguments<T1, T2>(this object?[]? args)
    {
        if (args is not { Length: 2 })
        {
            throw new ArgumentException("Expected 1 argument", nameof(args));
        }

        if (args[0] is not T1 arg1)
        {
            throw new ArgumentException($"Expected {typeof(T1).Name} as first argument", nameof(args));
        }

        if (args[1] is not T2 arg2)
        {
            throw new ArgumentException($"Expected {typeof(T2).Name} as second argument", nameof(args));
        }

        return (arg1, arg2);
    }
}