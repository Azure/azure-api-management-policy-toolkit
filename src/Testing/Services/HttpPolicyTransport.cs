// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

internal static class HttpPolicyTransport
{
    private static readonly TimeSpan s_maximumTimeout = TimeSpan.FromMilliseconds(uint.MaxValue - 1d);

    public static CancellationToken GetCancellationToken(GatewayContext context) =>
        context.Services.Resolve<HttpTransportState>()?.CancellationToken ?? CancellationToken.None;

    public static HttpTransportState GetState(GatewayContext context)
    {
        var state = context.Services.Resolve<HttpTransportState>();
        if (state is null)
        {
            state = new HttpTransportState();
            context.Services.Register(state);
        }

        return state;
    }

    public static IHttpClient GetClient(GatewayContext context) =>
        context.Services.Resolve<IHttpClient>()
        ?? throw new InvalidOperationException(
            "No IHttpClient registered. Register one via Context.Services.Register<IHttpClient>() " +
            "or override this policy with a callback. The emulator never creates a network client.");

    public static TimeSpan Timeout(int? seconds)
    {
        if (seconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), "HTTP request timeout cannot be negative.");
        }

        return ValidateTimeout(TimeSpan.FromSeconds(seconds ?? 60));
    }

    public static HttpTransportOptions ForwardOptions(ForwardRequestConfig? config)
    {
        if (config?.Timeout is not null && config.TimeoutMs is not null)
        {
            throw new ArgumentException("ForwardRequest accepts either Timeout or TimeoutMs, not both.", nameof(config));
        }

        return new HttpTransportOptions
        {
            TimeoutAppliesToHeadersOnly = true,
            Timeout = ValidateTimeout(config?.TimeoutMs is uint milliseconds
                ? TimeSpan.FromMilliseconds(milliseconds)
                : TimeSpan.FromSeconds(config?.Timeout ?? 300)),
            ContinueTimeout = config?.ContinueTimeout is uint continueSeconds
                ? TimeSpan.FromSeconds(continueSeconds)
                : null,
            FollowRedirects = config?.FollowRedirects ?? false,
            BufferRequestBody = config?.BufferRequestBody ?? false,
            BufferResponse = config?.BufferResponse ?? true
        };
    }

    public static MockResponse Send(GatewayContext context, IHttpClient client, HttpRequestMessage request) =>
        SendAsync(context, client, request).GetAwaiter().GetResult();

    public static void SendOneWay(GatewayContext context, IHttpClient client, HttpRequestMessage request)
    {
        var operation = SendOneWayAsync(context, client, request);
        if (operation.IsCompleted)
        {
            operation.GetAwaiter().GetResult();
        }
        else
        {
            GetState(context).TrackOneWayRequest(operation, context);
        }
    }

    public static void ObserveFault(Task operation)
    {
        _ = operation.ContinueWith(completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static async Task<MockResponse> SendAsync(GatewayContext context, IHttpClient client, HttpRequestMessage request)
    {
        HttpTransportCancellation? cancellation = null;
        Task<HttpResponseMessage>? pending = null;
        HttpResponseMessage? response = null;
        try
        {
            var options = GetOptions(request);
            cancellation = CreateCancellation(context, options);
            cancellation.Token.ThrowIfCancellationRequested();
            pending = client.SendAsync(request, cancellation.Token)
                ?? throw new InvalidOperationException("IHttpClient returned a null response task.");
            response = await pending.WaitAsync(cancellation.Token).ConfigureAwait(false);
            if (response is null)
            {
                throw new InvalidOperationException("IHttpClient returned a null response.");
            }

            if (options.TimeoutAppliesToHeadersOnly)
            {
                cancellation.CancelAfter(System.Threading.Timeout.InfiniteTimeSpan);
            }

            return await ReadResponseAsync(response, request.Method, cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            DisposeMessages(context, pending, response, request, cancellation);
        }
    }

    private static async Task SendOneWayAsync(GatewayContext context, IHttpClient client, HttpRequestMessage request)
    {
        HttpTransportCancellation? cancellation = null;
        Task<HttpResponseMessage>? pending = null;
        HttpResponseMessage? response = null;
        try
        {
            cancellation = CreateCancellation(context, GetOptions(request));
            cancellation.Token.ThrowIfCancellationRequested();
            pending = client.SendAsync(request, cancellation.Token)
                ?? throw new InvalidOperationException("IHttpClient returned a null response task.");
            response = await pending.WaitAsync(cancellation.Token).ConfigureAwait(false);
            if (response is null)
            {
                throw new InvalidOperationException("IHttpClient returned a null response.");
            }
        }
        finally
        {
            DisposeMessages(context, pending, response, request, cancellation);
        }
    }

    private static async Task<MockResponse> ReadResponseAsync(
        HttpResponseMessage response, HttpMethod method, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new MockResponse
        {
            StatusCode = (int)response.StatusCode,
            StatusReason = response.ReasonPhrase ?? string.Empty
        };
        foreach (var header in response.Headers)
        {
            result.Headers[header.Key] = header.Value.ToArray();
        }

        if (response.Content is not null)
        {
            foreach (var header in response.Content.Headers)
            {
                result.Headers[header.Key] = header.Value.ToArray();
            }
        }

        var status = result.StatusCode;
        var successfulConnect = method == HttpMethod.Connect && status is >= 200 and <= 299;
        if (status is >= 100 and <= 199 or 204 || successfulConnect)
        {
            result.Headers.Remove("Content-Length");
        }
        if (method == HttpMethod.Head || status is >= 100 and <= 199 or 204 or 205 or 304 || successfulConnect)
        {
            result.Body.Content = string.Empty;
            return result;
        }

        if (response.Content is not null)
        {
            var contentType = response.Content.Headers.ContentType;
            if (contentType?.CharSet is { } charset)
            {
                try
                {
                    Encoding.GetEncoding(charset.Trim('"'));
                }
                catch (Exception error) when (error is ArgumentException or NotSupportedException)
                {
                    throw new NotSupportedException($"HTTP backend response charset '{charset}' is not supported.", error);
                }
            }

            var read = response.Content.ReadAsStringAsync(cancellationToken);
            ObserveFault(read);
            var body = await read.WaitAsync(cancellationToken).ConfigureAwait(false);
            result.Body.Content = body;
            foreach (var header in response.Content.Headers)
            {
                result.Headers[header.Key] = header.Value.ToArray();
            }

            if (contentType is not null)
            {
                var normalizedType = MediaTypeHeaderValue.Parse(contentType.ToString());
                normalizedType.CharSet = Encoding.UTF8.WebName;
                result.Headers["Content-Type"] = [normalizedType.ToString()];
            }

            result.Headers["Content-Length"] = [Encoding.UTF8.GetByteCount(body).ToString(CultureInfo.InvariantCulture)];
        }

        return result;
    }

    private static HttpTransportOptions GetOptions(HttpRequestMessage request)
    {
        if (!request.Options.TryGetValue(HttpTransportOptions.Key, out var options) || options is null)
        {
            throw new InvalidOperationException("HTTP policy request is missing transport options.");
        }

        return options;
    }

    private static HttpTransportCancellation CreateCancellation(GatewayContext context, HttpTransportOptions options)
    {
        var timeout = ValidateTimeout(options.Timeout);
        var cancellation = GetState(context).CreateCancellation();
        if (timeout == TimeSpan.Zero)
        {
            cancellation.Cancel();
        }
        else
        {
            cancellation.CancelAfter(timeout);
        }

        return cancellation;
    }

    private static TimeSpan ValidateTimeout(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero || timeout > s_maximumTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout),
                $"HTTP timeout must be between zero and {s_maximumTimeout.TotalMilliseconds} milliseconds.");
        }

        return timeout;
    }

    private static void DisposeMessages(
        GatewayContext context,
        Task<HttpResponseMessage>? pending,
        HttpResponseMessage? response,
        HttpRequestMessage request,
        HttpTransportCancellation? cancellation)
    {
        if (pending is { IsCompleted: false })
        {
            // An uncooperative client may still be using the request after the policy timeout.
            var cleanup = pending.ContinueWith(completed =>
                DisposeCompletedMessages(completed, null, request, cancellation),
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            var report = cleanup.ContinueWith(completed =>
                context.Trace($"HTTP transport cleanup failed: {completed.Exception!.GetBaseException().Message}"),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            ObserveFault(report);
            return;
        }

        DisposeCompletedMessages(pending, response, request, cancellation);
    }

    private static void DisposeCompletedMessages(
        Task<HttpResponseMessage>? pending,
        HttpResponseMessage? response,
        HttpRequestMessage request,
        HttpTransportCancellation? cancellation)
    {
        try
        {
            if (response is not null)
            {
                response.Dispose();
            }
            else if (pending?.IsCompletedSuccessfully == true)
            {
                pending.Result?.Dispose();
            }

            if (pending?.IsFaulted == true)
            {
                _ = pending.Exception;
            }
        }
        finally
        {
            try
            {
                HttpTransportRequestBuilder.DisposeRequest(request);
            }
            finally
            {
                cancellation?.Dispose();
            }
        }
    }
}