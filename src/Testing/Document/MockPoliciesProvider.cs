// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public class MockPoliciesProvider<TSection> where TSection : class
{
    internal readonly SectionContextProxy<TSection> SectionContextProxy;

    internal MockPoliciesProvider(SectionContextProxy<TSection> proxy) => this.SectionContextProxy = proxy;
}