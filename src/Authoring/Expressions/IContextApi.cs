// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface IContextApi : IApi
{
    bool IsCurrentRevision { get; }
    string Revision { get; }
    string Version { get; }
}