// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record ValidateStatusCodeConfig
{
    public required string UnspecifiedStatusCodeAction { get; init; }
    public string? ErrorVariableName { get; init; }
    public ValidateStatusCode[]? StatusCodes { get; init; }
}

public record ValidateStatusCode
{
    public required uint Code { get; init; }
    public required string Action { get; init; }
}