// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface IBackend
{
    string AzureRegion { get; }

    string Id { get; }

    /// <summary>
    /// The type of the backend, "Single" or "Pool". The gateway exposes it as a string.
    /// </summary>
    string Type { get; }
}
