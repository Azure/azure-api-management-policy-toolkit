// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record XmlToJsonConfig
{
    public required string Kind { get; init; }
    public required string Apply { get; init; }
    public bool? ConsiderAcceptHeader { get; init; }
    public bool? AlwaysArrayChildElements { get; init; }
}