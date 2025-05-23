// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public class LoggerStore
{
    private readonly Dictionary<string, Logger> _loggers = new();

    public Logger Add(string loggerId)
    {
        var logger = new Logger(loggerId);
        this.Add(logger);
        return logger;
    }

    public void Add(Logger logger)
    {
        if (!_loggers.TryAdd(logger.LoggerId, logger))
        {
            throw new Exception($"Logger with id {logger.LoggerId} already exists.");
        }
    }

    public bool TryGet(string loggerId, [NotNullWhen(true)] out Logger logger) =>
        _loggers.TryGetValue(loggerId, out logger!);
}