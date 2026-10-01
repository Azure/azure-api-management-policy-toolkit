// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockResponse : MockMessage, IResponse
{
    private int _statusCode = 200;
    private string _statusReason = "OK";
    private long _statusCodeWriteVersion;
    private long _statusReasonWriteVersion;

    IMessageBody IResponse.Body => Body;

    IReadOnlyDictionary<string, string[]> IResponse.Headers => Headers;

    public int StatusCode
    {
        get => _statusCode;
        set
        {
            _statusCode = value;
            Interlocked.Increment(ref _statusCodeWriteVersion);
        }
    }

    public string StatusReason
    {
        get => _statusReason;
        set
        {
            _statusReason = value;
            Interlocked.Increment(ref _statusReasonWriteVersion);
        }
    }

    internal long StatusCodeWriteVersion => Volatile.Read(ref _statusCodeWriteVersion);
    internal long StatusReasonWriteVersion => Volatile.Read(ref _statusReasonWriteVersion);
}