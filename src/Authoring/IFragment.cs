// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Interface for policy fragments. Policy fragments are reusable policy elements 
/// that can be included in policy definitions using the include-fragment policy.
/// </summary>
public interface IFragment
{
    /// <summary>
    /// Defines the policy elements that make up this fragment.
    /// The policies defined in this method will be compiled directly into the fragment.
    /// </summary>
    /// <param name="context">The fragment context providing access to all policy operations.</param>
    void Fragment(IFragmentContext context);
}