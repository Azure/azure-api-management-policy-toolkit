// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;

/// <summary>
/// Represents the scope level at which a policy document is applied.
/// Scopes are evaluated from outermost (Global) to innermost (Operation) for
/// inbound/backend sections, and reversed for outbound/on-error sections.
/// </summary>
public enum PolicyScope
{
    Global,
    Workspace,
    Product,
    Api,
    Operation
}
