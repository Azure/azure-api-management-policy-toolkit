// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public static class X509Certificate2Extensions
{
    /// <summary>
    /// Performs an X.509 chain validation without checking certificate revocation status.
    /// </summary>
    public static bool VerifyNoRevocation(this X509Certificate2 input)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(input);
    }
}
