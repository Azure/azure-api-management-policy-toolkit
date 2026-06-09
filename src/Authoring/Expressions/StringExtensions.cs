// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Implementations;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public static class StringExtensions
{
    public static BasicAuthCredentials? AsBasic(this string? value)
        => ImplementationContext.Default.GetService<IBasicAuthCredentialsParser>().Parse(value);

    public static bool TryParseBasic(this string? value, [MaybeNullWhen(false)] out BasicAuthCredentials credentials)
        => (credentials = value.AsBasic()) is not null;

    public static Jwt? AsJwt(this string? value)
        => ImplementationContext.Default.GetService<IJwtParser>().Parse(value);

    public static bool TryParseJwt(this string? value, [MaybeNullWhen(false)] out Jwt token)
        => (token = value.AsJwt()) is not null;

    /// <summary>
    /// Returns the string itself as a nullable string, treating it as the first (and only) element.
    /// This extension shadows <see cref="Enumerable.FirstOrDefault{TSource}(IEnumerable{TSource})"/>
    /// so that code calling <c>.FirstOrDefault()</c> on a <c>string?</c> (from Claims.GetValueOrDefault)
    /// compiles correctly, returning <c>string?</c> rather than <c>char?</c>.
    /// </summary>
    public static string? FirstOrDefault(this string s) => s;
}