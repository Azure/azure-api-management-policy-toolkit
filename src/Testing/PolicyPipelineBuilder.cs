// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;

/// <summary>
/// Fluent builder for composing multi-scope policy pipelines.
/// Each scope can have one <see cref="IDocument"/> whose sections are executed
/// in the correct order when the pipeline runs.
/// </summary>
public class PolicyPipelineBuilder
{
    private readonly Dictionary<PolicyScope, IDocument> _policies = new();
    private readonly GatewayContext _context = new();

    public static PolicyPipelineBuilder Create() => new();

    /// <summary>
    /// Registers a policy document at the specified scope.
    /// Replaces any previously registered document for that scope.
    /// </summary>
    public PolicyPipelineBuilder AddPolicy(PolicyScope scope, IDocument document)
    {
        _policies[scope] = document;
        return this;
    }

    /// <summary>
    /// Configures the shared <see cref="GatewayContext"/> used by all scopes.
    /// </summary>
    public PolicyPipelineBuilder ConfigureContext(Action<GatewayContext> configure)
    {
        configure(_context);
        return this;
    }

    /// <summary>
    /// Builds the pipeline. The resulting <see cref="PolicyPipeline"/> shares
    /// a single <see cref="GatewayContext"/> across all scope documents.
    /// </summary>
    public PolicyPipeline Build() => new PolicyPipeline(_policies, _context);
}
