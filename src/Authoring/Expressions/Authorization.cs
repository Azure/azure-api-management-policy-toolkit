// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

/// <summary>
/// Represents an authorization object with access token and claims.
/// </summary>
public class Authorization
{
    /// <summary>
    /// Gets the bearer access token to authorize a backend HTTP request.
    /// </summary>
    public string AccessToken { get; }

    /// <summary>
    /// Gets the claims returned from the authorization server's token response API.
    /// </summary>
    public IReadOnlyDictionary<string, object> Claims { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Authorization"/> class.
    /// </summary>
    /// <param name="accessToken">The bearer access token.</param>
    /// <param name="claims">The claims returned from the authorization server's token response API.</param>
    public Authorization(string accessToken, IReadOnlyDictionary<string, object> claims)
    {
        AccessToken = accessToken;
        Claims = claims;
    }
}
