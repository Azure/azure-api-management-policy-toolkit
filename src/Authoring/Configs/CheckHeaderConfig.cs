// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record CheckHeaderConfig
{
    public required string Name { get; init; }
    public required int FailCheckHttpCode { get; init; }
    public required string FailCheckErrorMessage { get; init; }
    public required bool IgnoreCase { get; init; }
    public required string[] Values { get; init; }
}