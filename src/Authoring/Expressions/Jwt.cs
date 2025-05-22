// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface Jwt
{
    string Id { get; }
    public string Algorithm { get; }
    public string Issuer { get; }
    public string Subject { get; }
    public string Type { get; }
    public IEnumerable<string> Audiences { get; }
    public IReadOnlyDictionary<string, string[]> Claims { get; }
    public DateTime? ExpirationTime { get; }
    public DateTime? NotBefore { get; }
    public DateTime? IssuedAt { get; }
}