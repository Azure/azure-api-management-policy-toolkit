// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public class LoggerStore
{
    private readonly Dictionary<string, Logger> _loggers = new();

    internal readonly SynchronizedList<TraceEvent> TracesInternal = new();
    internal readonly SynchronizedList<MetricEvent> MetricsInternal = new();

    /// <summary>
    /// Gets an immutable snapshot of trace policy telemetry recorded in this gateway context.
    /// </summary>
    public ImmutableArray<TraceEvent> Traces => TracesInternal.Snapshot();

    /// <summary>
    /// Gets an immutable snapshot of emit-metric policy telemetry recorded in this gateway context.
    /// </summary>
    public ImmutableArray<MetricEvent> Metrics => MetricsInternal.Snapshot();

    public Logger Add(string loggerId)
    {
        var logger = new Logger(loggerId);
        this.Add(logger);
        return logger;
    }

    public void Add(Logger logger)
    {
        lock (_loggers)
        {
            if (!_loggers.TryAdd(logger.LoggerId, logger))
            {
                throw new Exception($"Logger with id {logger.LoggerId} already exists.");
            }
        }
    }

    public bool TryGet(string loggerId, [NotNullWhen(true)] out Logger logger)
    {
        lock (_loggers)
        {
            return _loggers.TryGetValue(loggerId, out logger!);
        }
    }
}