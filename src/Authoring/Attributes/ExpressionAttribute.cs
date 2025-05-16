// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

[AttributeUsage(AttributeTargets.Method)]
public class ExpressionAttribute([CallerFilePath] string sourceFilePath = "") : Attribute
{
    public string SourceFilePath { get; } = sourceFilePath;
}