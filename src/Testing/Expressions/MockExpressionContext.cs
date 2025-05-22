// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockExpressionContext : IExpressionContext
{
    static MockExpressionContext()
    {
        MockExtensions.SetDefaultServices();
    }

    public Guid RequestId { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan Elapsed { get; set; } = TimeSpan.Zero;
    public bool Tracing { get; set; } = false;

    public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();
    IReadOnlyDictionary<string, object> IExpressionContext.Variables => Variables;

    public MockContextApi Api { get; set; } = new MockContextApi();
    IContextApi IExpressionContext.Api => Api;

    public MockRequest Request { get; set; } = new MockRequest();
    IRequest IExpressionContext.Request => Request;

    public MockResponse Response { get; set; } = new MockResponse();
    IResponse IExpressionContext.Response => Response;

    public MockSubscription Subscription { get; set; } = new MockSubscription();
    ISubscription IExpressionContext.Subscription => Subscription;

    public MockUser User { get; set; } = new MockUser();
    IUser IExpressionContext.User => User;

    public MockDeployment Deployment { get; set; } = new MockDeployment();
    IDeployment IExpressionContext.Deployment => Deployment;

    public MockLastError LastError { get; set; } = new MockLastError();
    ILastError IExpressionContext.LastError => LastError;

    public MockOperation Operation { get; set; } = new MockOperation();
    IOperation IExpressionContext.Operation => Operation;

    public MockProduct Product { get; set; } = new MockProduct();
    IProduct IExpressionContext.Product => Product;

    public Action<string> Trace { get; set; } = (message) => { };
}