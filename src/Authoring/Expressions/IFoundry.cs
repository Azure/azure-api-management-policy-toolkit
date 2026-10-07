// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface IFoundry
{
    /// <summary>
    /// The model deployment ID in Microsoft Foundry associated with the request.
    /// </summary>
    string Deployment { get; }
}
