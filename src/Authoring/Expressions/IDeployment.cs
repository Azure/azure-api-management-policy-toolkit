// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface IDeployment
{
    IGateway Gateway { get; }

    string GatewayId { get; }

    string Region { get; }

    string ServiceId { get; }

    string ServiceName { get; }

    ISustainabilityInfo SustainabilityInfo { get; }

    IReadOnlyDictionary<string, X509Certificate2> Certificates { get; }
}