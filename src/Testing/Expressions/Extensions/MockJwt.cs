// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockJwt : Jwt
{
    public string Id { get; set; }
    public string Algorithm { get; set; }
    public string Issuer { get; set; }
    public string Subject { get; set; }
    public string Type { get; set; }
    public IEnumerable<string> Audiences { get; set; }
    public IReadOnlyDictionary<string, string[]> Claims { get; set; }
    public DateTime? ExpirationTime { get; set; }
    public DateTime? NotBefore { get; set; }
    public DateTime? IssuedAt { get; set; }
}