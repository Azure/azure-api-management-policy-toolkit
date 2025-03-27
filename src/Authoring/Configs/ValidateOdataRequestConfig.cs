// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record ValidateOdataRequestConfig
{
    public string? ErrorVariableName { get; init; }
    public string? DefaultOdataVersion { get; init; }
    public string? MinOdataVersion { get; init; }
    public string? MaxOdataVersion { get; init; }
    public int? MaxSize { get; init; }
}