// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

public class BadRuntimeConfigurationException(string message) : Exception(message)
{
    public required string Policy { get; init; }
}
