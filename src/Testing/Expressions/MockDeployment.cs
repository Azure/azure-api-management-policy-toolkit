// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockDeployment : IDeployment
{
    public MockGateway Gateway { get; set; } = new MockGateway();
    IGateway IDeployment.Gateway => Gateway;

    public string GatewayId { get; set; } = "managed";
    public string Region { get; set; } = "eastus";
    public string ServiceId { get; set; } = "contoso-dev-apim";
    public string ServiceName { get; set; } = "contoso-dev-apim";

    public MockSustainabilityInfo SustainabilityInfo { get; set; } = new MockSustainabilityInfo();
    ISustainabilityInfo IDeployment.SustainabilityInfo => SustainabilityInfo;

    public Dictionary<string, X509Certificate2> Certificates { get; set; } = new Dictionary<string, X509Certificate2>();
    IReadOnlyDictionary<string, X509Certificate2> IDeployment.Certificates => Certificates;
}