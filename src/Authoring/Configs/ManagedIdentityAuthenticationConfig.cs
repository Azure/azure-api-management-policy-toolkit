// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

public record ManagedIdentityAuthenticationConfig : IAuthenticationConfig
{
    /// <summary>
    /// Required. The resource for which the token is requested. This is the resource ID or URL used when requesting a token from Azure AD.<br/>
    /// Example: https://management.azure.com/ or https://graph.microsoft.com/
    /// </summary>
    [ExpressionAllowed]
    public required string Resource { get; init; }

    /// <summary>
    /// Optional. The client ID of the user-assigned managed identity.<br/>
    /// If this property is not specified, the system-assigned managed identity is used.
    /// </summary>
    [ExpressionAllowed]
    public string? ClientId { get; init; }

    /// <summary>
    /// Optional. The name of the variable where the access token will be stored for later use in the policy.<br/>
    /// If this property is not specified, the token is not saved to a variable.
    /// </summary>
    public string? OutputTokenVariableName { get; init; }

    /// <summary>
    /// Optional. When set to true, failures to obtain a token are ignored and processing continues to the next policy.<br/>
    /// Default is false.
    /// </summary>
    public bool? IgnoreError { get; init; }
}