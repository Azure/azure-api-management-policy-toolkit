// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record ValidateHeadersConfig
{
    public required string SpecifiedHeaderAction { get; init; }
    public required string UnspecifiedHeaderAction { get; init; }
    public string? ErrorsVariableName { get; init; }
    public ValidateHeader[]? Headers { get; init; }
}

public record ValidateHeader
{
    public required string Name { get; init; }
    public required string Action { get; init; }
}