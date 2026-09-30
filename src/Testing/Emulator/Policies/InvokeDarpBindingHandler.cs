// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class InvokeDarpBindingHandler : PolicyHandler<InvokeDarpBindingConfig>
{
    public override string PolicyName => nameof(IInboundContext.InvokeDarpBinding);

    protected override void Handle(GatewayContext context, InvokeDarpBindingConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Name, nameof(config.Name));
        if (config.Operation is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(config.Operation, nameof(config.Operation));
        }

        if (config.ResponseVariableName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(config.ResponseVariableName, nameof(config.ResponseVariableName));
        }

        var timeout = config.Timeout ?? 5;
        if (timeout is < 1 or > 240)
        {
            throw new ArgumentOutOfRangeException(nameof(config.Timeout), timeout, "Timeout must be between 1 and 240 seconds.");
        }

        if (config.Template is not null && !string.Equals(config.Template, "Liquid", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only the Liquid template is supported.", nameof(config.Template));
        }

        if (config.ContentType is not null && !string.Equals(config.ContentType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only application/json content is supported.", nameof(config.ContentType));
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (config.MetaData is not null)
        {
            foreach (var item in config.MetaData)
            {
                if (item is null)
                {
                    throw new ArgumentException("Metadata items cannot be null.", nameof(config.MetaData));
                }

                ArgumentException.ThrowIfNullOrWhiteSpace(item.Key, nameof(config.MetaData));
                ArgumentNullException.ThrowIfNull(item.Value, nameof(config.MetaData));
                if (!metadata.TryAdd(item.Key, item.Value))
                {
                    throw new ArgumentException($"Duplicate metadata key '{item.Key}'.", nameof(config.MetaData));
                }
            }
        }

        var request = new DaprBindingRequest(
            config.Name, config.Operation, new ReadOnlyDictionary<string, string>(metadata),
            config.Data ?? context.Request.Body.As<string>(preserveContent: true),
            config.Template, config.ContentType, TimeSpan.FromSeconds(timeout));
        var service = context.Services.Resolve<IDaprBindingService>() ?? throw new InvalidOperationException(
            "No IDaprBindingService registered. Register one via test.Context.Services.Register<IDaprBindingService>(service) " +
            "or use a policy callback override.");

        if (config.ResponseVariableName is not null)
        {
            context.Variables[config.ResponseVariableName] = null!;
        }

        using var cancellation = new CancellationTokenSource(request.Timeout);
        IResponse response;
        try
        {
            response = service.InvokeAsync(request, cancellation.Token)
                .WaitAsync(cancellation.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException error) when (cancellation.IsCancellationRequested)
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
            throw new InvalidOperationException("IDaprBindingService returned a null response.");
        }

        if (config.ResponseVariableName is not null)
        {
            context.Variables[config.ResponseVariableName] = DaprServiceResponse.ForVariable(response);
        }

        if (response.StatusCode is < 200 or >= 300)
        {
            var error = new HttpRequestException(
                $"Dapr binding '{request.Name}' returned HTTP {response.StatusCode} {response.StatusReason}.",
                null, (System.Net.HttpStatusCode)response.StatusCode);
            if (config.IgnoreError != true)
            {
                throw error;
            }

            context.Trace($"{PolicyName} ignored an error: {error.Message}");
        }
    }
}
