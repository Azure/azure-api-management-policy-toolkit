// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

public interface IRateLimiter
{
    Task<bool> TryConsumeAsync(string key, int permits = 1, CancellationToken cancellationToken = default);
}
