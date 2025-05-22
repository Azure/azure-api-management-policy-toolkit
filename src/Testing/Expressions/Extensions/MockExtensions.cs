// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Implementations;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public static class MockExtensions
{
    public static void SetBasicAuthCredentialParser(IBasicAuthCredentialsParser parser)
        => ImplementationContext.Default.SetService(parser);

    public static void SetDefaultBasicAuthCredentialParser()
        => SetBasicAuthCredentialParser(new BasicAuthCredentialsParser());

    public static void SetJwtParser(IJwtParser parser)
        => ImplementationContext.Default.SetService(parser);

    public static void SetDefaultJwtParser()
        => SetJwtParser(new JwtParser());

    public static void SetUrlContentEncoder(IUrlContentEncoder encoder)
        => ImplementationContext.Default.SetService(encoder);

    public static void SetDefaultUrlContentEncoder()
        => SetUrlContentEncoder(new UrlContentEncoder());

    public static void SetDefaultServices()
    {
        SetDefaultBasicAuthCredentialParser();
        SetDefaultJwtParser();
        SetDefaultUrlContentEncoder();
    }
}