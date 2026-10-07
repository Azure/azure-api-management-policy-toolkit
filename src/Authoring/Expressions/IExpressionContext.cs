// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface IExpressionContext
{
    IContextApi Api { get; }
    /// <summary>
    /// The backend entity the request is sent to. Null until one is selected, for example with set-backend-service.
    /// </summary>
    IBackend? Backend { get; }
    IDeployment Deployment { get; }
    TimeSpan Elapsed { get; }
    ILastError LastError { get; }
    IOperation Operation { get; }
    /// <summary>
    /// The product of the subscription the request was made with. Null for a request without a subscription.
    /// </summary>
    IProduct? Product { get; }
    IRequest Request { get; }
    Guid RequestId { get; }
    IResponse Response { get; }
    ISubscription Subscription { get; }
    DateTime Timestamp { get; }
    bool Tracing { get; }
    IUser User { get; }
    IReadOnlyDictionary<string, object> Variables { get; }
    /// <summary>
    /// The workspace of the API. Null for an API that is not in a workspace.
    /// </summary>
    IWorkspace? Workspace { get; }
    Action<string> Trace { get; }

    /// <summary>
    /// Returns a named value that resolves at runtime. Compiles to {{name}} in XML.
    /// Supports implicit conversion to string, int, bool, etc.
    /// </summary>
    dynamic NamedValue(string name);
}
