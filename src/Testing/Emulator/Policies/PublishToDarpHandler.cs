// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class PublishToDarpHandler : PolicyHandler<PublishToDarpConfig>
{
    public override string PolicyName => nameof(IInboundContext.PublishToDarp);

    protected override void Handle(GatewayContext context, PublishToDarpConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Topic, nameof(config.Topic));
        ArgumentNullException.ThrowIfNull(config.Content, nameof(config.Content));

        var component = config.PubSubName;
        var topic = config.Topic;
        if (component is null)
        {
            var separator = topic.IndexOf('/');
            if (separator < 0)
            {
                throw new ArgumentException(
                    "Topic must include a pubsub-name/topic-name when PubSubName is omitted.", nameof(config.Topic));
            }

            component = topic[..separator];
            topic = topic[(separator + 1)..];
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(component, nameof(config.PubSubName));
        ArgumentException.ThrowIfNullOrWhiteSpace(topic, nameof(config.Topic));
        if (config.ResponseVariableName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(config.ResponseVariableName, nameof(config.ResponseVariableName));
        }

        var timeout = config.Timeout ?? 5;
        if (timeout is < 1 or > 240)
        {
            throw new ArgumentOutOfRangeException(
                nameof(config.Timeout), timeout, "Timeout must be between 1 and 240 seconds.");
        }

        if (config.Template is not null && !string.Equals(config.Template, "Liquid", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only the Liquid template is supported.", nameof(config.Template));
        }

        if (config.ContentType is not null && !string.Equals(config.ContentType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only application/json content is supported.", nameof(config.ContentType));
        }

        var request = new DaprPublishRequest(
            component, topic, config.Content, config.Template, config.ContentType, TimeSpan.FromSeconds(timeout));
        var service = context.Services.Resolve<IDaprPubSubService>() ?? throw new InvalidOperationException(
            "No IDaprPubSubService registered. Register one via test.Context.Services.Register<IDaprPubSubService>(service) " +
            "or use a policy callback override.");

        if (config.ResponseVariableName is not null)
        {
            context.Variables[config.ResponseVariableName] = null!;
        }

        using var cancellation = HttpPolicyTransport.GetState(context).CreateCancellation();
        cancellation.CancelAfter(request.Timeout);
        IResponse response;
        try
        {
            var operation = service.PublishAsync(request, cancellation.Token)
                ?? throw new InvalidOperationException("The Dapr pub/sub service returned a null task.");
            HttpPolicyTransport.ObserveFault(operation);
            response = operation.WaitAsync(cancellation.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (HttpPolicyTransport.GetCancellationToken(context).IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException error) when (cancellation.Token.IsCancellationRequested)
        {
            var timeoutError = new TimeoutException($"{PolicyName} timed out after {timeout} seconds.", error);
            if (config.IgnoreError != true)
            {
                throw timeoutError;
            }

            context.Trace($"{PolicyName} ignored an error: {timeoutError.Message}");
            return;
        }
        catch (HttpRequestException error) when (config.IgnoreError == true)
        {
            context.Trace($"{PolicyName} ignored an error: {error.Message}");
            return;
        }
        catch (TimeoutException error) when (config.IgnoreError == true)
        {
            context.Trace($"{PolicyName} ignored an error: {error.Message}");
            return;
        }
        catch (OperationCanceledException error) when (config.IgnoreError == true)
        {
            context.Trace($"{PolicyName} ignored an error: {error.Message}");
            return;
        }

        if (response is null)
        {
            throw new InvalidOperationException("IDaprPubSubService returned a null response.");
        }

        if (config.ResponseVariableName is not null)
        {
            context.Variables[config.ResponseVariableName] = DaprServiceResponse.ForVariable(response);
        }

        if (response.StatusCode is < 200 or >= 300)
        {
            var error = new HttpRequestException(
                $"Dapr publish to '{request.PubSubName}/{request.Topic}' returned HTTP {response.StatusCode} {response.StatusReason}.",
                null, (System.Net.HttpStatusCode)response.StatusCode);
            if (config.IgnoreError != true)
            {
                throw error;
            }

            context.Trace($"{PolicyName} ignored an error: {error.Message}");
        }
    }
}