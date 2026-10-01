// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Marks a policy expression method, or a class of expression helper methods and constants (an expression helper
/// library) whose members can be used from policy expressions, including from another project.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class ExpressionAttribute([CallerFilePath] string sourceFilePath = "") : Attribute
{
    public string SourceFilePath { get; } = sourceFilePath;
}