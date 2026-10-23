// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface BasicAuthCredentials
{
    /// <summary>
    /// The user, named UserId as in API Management's expression API.
    /// </summary>
    public string UserId { get; }
    public string Password { get; }
}