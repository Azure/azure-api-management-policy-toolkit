// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Injected service for invoke-dapr-binding. Register an implementation with <see cref="ServiceRegistry"/>.
/// No Dapr runtime or network client is provided by the emulator.
/// </summary>
public interface IDaprBindingService
{
    /// <summary>
    /// Invokes the binding with the supplied data, metadata, and optional template settings.
    /// Implementations own template rendering and return the Dapr response, including error responses.
    /// Responses stored in variables are normalized for gateway policies without consuming their body.
    /// Transport failures may be reported as <see cref="HttpRequestException"/>,
    /// <see cref="TimeoutException"/>, or <see cref="OperationCanceledException"/>.
    /// </summary>
    Task<IResponse> InvokeAsync(DaprBindingRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Captures the evaluated configuration sent to a Dapr binding service.
/// </summary>
/// <param name="Name">Target binding name.</param>
/// <param name="Operation">Binding operation, or null when omitted.</param>
/// <param name="Metadata">A case-sensitive snapshot of binding metadata.</param>
/// <param name="Data">Explicit data, or the preserved request body when data is omitted.</param>
/// <param name="Template">Template engine to apply to data, or null for literal data.</param>
/// <param name="ContentType">Configured content type, or null when omitted.</param>
/// <param name="Timeout">Invocation deadline, converted from the authoring config's seconds.</param>
public sealed record DaprBindingRequest(
    string Name,
    string? Operation,
    IReadOnlyDictionary<string, string> Metadata,
    string Data,
    string? Template,
    string? ContentType,
    TimeSpan Timeout);

internal static class DaprServiceResponse
{
    internal static MockResponse ForVariable(IResponse response)
    {
        if (response is MockResponse mockResponse)
        {
            return mockResponse;
        }

        var result = new MockResponse
        {
            StatusCode = response.StatusCode,
            StatusReason = response.StatusReason
        };
        foreach (var header in response.Headers)
        {
            result.Headers[header.Key] = header.Value.ToArray();
        }

        result.Body.Content = response.Body.As<string>(preserveContent: true);
        return result;
    }
}
