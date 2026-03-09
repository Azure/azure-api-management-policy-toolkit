// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling;

public record DecompileOptions
{
    public string? Scope { get; init; }
    /// <summary>
    /// When set, emitted as the first positional argument in the [Document] attribute.
    /// For policies this is typically the relative path (e.g. "apis/myapi/operations/op1/policy.xml").
    /// For fragments the fragment ID is used instead.
    /// </summary>
    public string? DocumentId { get; init; }
}
