// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public record EventHubEvent(string Value, string? PartitionId = null, string? PartitionKey = null);