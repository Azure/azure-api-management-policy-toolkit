// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

public class PolicyException(Exception e) : Exception(e.Message, e)
{
    public required string Policy { get; init; }
    public required string Section { get; init; }
    public required object?[]? PolicyArgs { get; init; }
}