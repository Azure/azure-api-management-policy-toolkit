// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public class BackendStore
{
    private readonly Dictionary<string, Backend> _backends = new();

    public BackendStore Add(string id, string url)
    {
        var backend = new Backend(id, url);
        Add(backend);
        return this;
    }

    public void Add(Backend backend)
    {
        if (!_backends.TryAdd(backend.Id, backend))
        {
            throw new Exception($"Backend with id '{backend.Id}' already exists.");
        }
    }

    public bool TryGet(string id, [NotNullWhen(true)] out Backend? backend) =>
        _backends.TryGetValue(id, out backend);
}
