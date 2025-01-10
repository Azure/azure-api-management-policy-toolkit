// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public record CacheValue(object Value, uint Duration = 0);