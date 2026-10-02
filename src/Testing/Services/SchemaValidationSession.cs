// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

internal sealed class SchemaValidationSession
{
    internal const int MaximumBodySize = 4 * 1024 * 1024;

    private readonly GatewayContext _context;
    private readonly string _policy;
    private readonly string? _errorsVariable;
    private readonly List<SchemaValidationError> _errors = [];

    internal SchemaValidationSession(GatewayContext context, string policy, string? errorsVariable)
    {
        _context = context;
        _policy = policy;
        _errorsVariable = errorsVariable;
        if (errorsVariable is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(errorsVariable);
        }

        Section = context.CurrentSectionName switch
        {
            nameof(IInboundContext) => "inbound",
            nameof(IOutboundContext) => "outbound",
            nameof(IOnErrorContext) => "on-error",
            _ => throw new InvalidOperationException($"{policy} requires an authored validation section."),
        };
    }

    internal string Section { get; }
    internal bool IsRequest => Section == "inbound";
    internal string BodyType => IsRequest ? "RequestBody" : "ResponseBody";
    internal MockMessage Message => IsRequest ? _context.Request : _context.Response;

    internal static string Action(string? action, string parameter)
    {
        if (action is not ("ignore" or "detect" or "prevent"))
        {
            throw new ArgumentException(
                "Validation actions must be ignore, detect, or prevent. allow/deny are not APIM validation actions.",
                parameter);
        }
        return action;
    }

    internal static void ValidateSize(int maxSize, string parameter)
    {
        if (maxSize is < 0 or > MaximumBodySize)
        {
            throw new ArgumentOutOfRangeException(parameter, maxSize,
                $"Validation size limits must be between 0 and {MaximumBodySize} bytes.");
        }
    }

    internal void CheckSize(int maxSize, string action)
    {
        if (action == "ignore")
        {
            return;
        }

        EnsureUtf8(Message);
        var size = Encoding.UTF8.GetByteCount(Message.Body.Content ?? string.Empty);
        // MockBody represents UTF-8 text; a forged Content-Length must not bypass the actual byte limit.
        if (size > maxSize)
        {
            Add(string.Empty, BodyType, "SizeLimit",
                $"{BodyType} is {size} bytes long and exceeds the configured limit of {maxSize} bytes.", action);
        }
    }

    internal void Add(string name, string type, string rule, string details, string action)
        => AddErrors(name, type, rule, [details], action);

    internal void AddErrors(string name, string type, string rule, IReadOnlyList<string> details, string action)
    {
        Action(action, nameof(action));
        ArgumentNullException.ThrowIfNull(details);
        if (action == "ignore" || details.Count == 0)
        {
            return;
        }

        foreach (var detail in details)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(detail);
            var error = new SchemaValidationError(name, type, rule, detail, action);
            _errors.Add(error);
            var trace = $"{_policy}: {JsonSerializer.Serialize(error)}";
            _context.LoggerStore.TracesInternal.Add(new TraceEvent(
                _policy, trace, "error", ImmutableDictionary<string, string>.Empty));
            _context.Trace(trace);
        }
        if (action != "prevent")
        {
            return;
        }

        Complete();
        var status = IsRequest ? 400 : 502;
        ResponseUtilities.Overwrite(_context.Response, status, IsRequest ? "Bad Request" : "Bad Gateway");
        _context.Response.Headers["Content-Type"] = ["application/json"];
        _context.Response.Body.Content = JsonSerializer.Serialize(new
        {
            statusCode = status,
            message = IsRequest ? "Request validation failed." : "The response failed validation.",
        });
        _context.LastError.Source = _policy;
        _context.LastError.Section = Section;
        _context.LastError.Reason = IsRequest ? "Bad request" : "Response not allowed";
        _context.LastError.Message = details[0];
        _context.LastError.HttpErrorCode = status;
        throw new FinishSectionProcessingException();
    }

    internal void Complete()
    {
        if (_errorsVariable is not null)
        {
            _context.Variables[_errorsVariable] = _errors.ToArray();
        }
    }

    internal static string[] HeaderValues(IEnumerable<KeyValuePair<string, string[]>> headers, string name)
    {
        var values = new List<string>();
        foreach (var header in headers.Where(header => string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase)))
        {
            ArgumentNullException.ThrowIfNull(header.Value);
            foreach (var value in header.Value)
            {
                ArgumentNullException.ThrowIfNull(value);
                values.Add(value);
            }
        }
        return values.ToArray();
    }

    internal static (string Type, string? Charset, string? Error) ContentType(
        IEnumerable<KeyValuePair<string, string[]>> headers)
    {
        var values = HeaderValues(headers, "Content-Type");
        if (values.Length == 0 || values is [""])
        {
            return (string.Empty, null, null);
        }
        if (values.Length != 1 || !MediaTypeHeaderValue.TryParse(values[0], out var parsed) ||
            string.IsNullOrEmpty(parsed.MediaType) || parsed.MediaType.Contains('*'))
        {
            return (string.Empty, null, "Content-Type must contain one valid media type.");
        }
        return (parsed.MediaType.ToLowerInvariant(), parsed.CharSet?.Trim('"'), null);
    }

    internal static string MediaType(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        if (!MediaTypeHeaderValue.TryParse(type, out var parsed) || string.IsNullOrEmpty(parsed.MediaType))
        {
            throw new ArgumentException($"Invalid configured media type '{type}'.", nameof(type));
        }
        if (parsed.MediaType.Contains('*'))
        {
            throw new NotSupportedException("Wildcard media type schema selection is unsupported; register concrete API media types.");
        }
        return parsed.MediaType.ToLowerInvariant();
    }

    internal static void EnsureUtf8(MockMessage message)
    {
        var encodings = HeaderValues(message.Headers, "Content-Encoding")
            .SelectMany(value => value.Split(',')).Select(value => value.Trim());
        if (encodings.Any(encoding => !string.Equals(encoding, "identity", StringComparison.OrdinalIgnoreCase)))
        {
            throw new NotSupportedException(
                "Content-Encoding cannot be validated or decompressed by the UTF-8 text emulator. Supply decoded identity content.");
        }

        var (_, charset, _) = ContentType(message.Headers);
        if (charset is not null && !string.Equals(charset, "utf-8", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(charset, "utf8", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Content-Type charset '{charset}' cannot be measured faithfully: emulator bodies are UTF-8 text.");
        }
    }
}