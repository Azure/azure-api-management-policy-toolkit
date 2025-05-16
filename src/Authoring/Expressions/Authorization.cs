// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

/// <summary>
/// Represents an authorization object containing an access token and related claims.
/// </summary>
public class Authorization
{
    /// <summary>
    /// Gets the access token used to authorize a backend HTTP request.
    /// </summary>
    public string AccessToken { get; }

    /// <summary>
    /// Gets a dictionary containing claims associated with the access token.
    /// </summary>
    public IReadOnlyDictionary<string, object> Claims { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Authorization"/> class.
    /// </summary>
    /// <param name="accessToken">The access token used to authorize a backend HTTP request.</param>
    /// <param name="claims">A dictionary containing claims associated with the access token.</param>
    public Authorization(string accessToken, IReadOnlyDictionary<string, object> claims)
    {
        AccessToken = accessToken;
        Claims = claims;
    }
}