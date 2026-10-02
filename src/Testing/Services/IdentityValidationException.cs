// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

internal sealed class IdentityValidationException(string reason, string message) : SecurityTokenException(message)
{
    internal string Reason { get; } = reason;
}
