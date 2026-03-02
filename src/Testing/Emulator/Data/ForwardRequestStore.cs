// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public class ForwardRequestStore
{
    private readonly Queue<MockBackendResponse> _responses = new();
    private MockBackendResponse? _default;

    public ForwardRequestStore Returns(MockBackendResponse response)
    {
        _responses.Enqueue(response);
        return this;
    }

    public ForwardRequestStore ReturnsDefault(MockBackendResponse response)
    {
        _default = response;
        return this;
    }

    internal MockBackendResponse? GetNext() =>
        _responses.Count > 0 ? _responses.Dequeue() : _default;
}
