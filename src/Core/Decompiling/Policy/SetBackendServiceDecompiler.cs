// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class SetBackendServiceDecompiler : IPolicyDecompiler
{
    public string PolicyName => "set-backend-service";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddOptionalStringProp(props, element, "base-url", "BaseUrl");
        context.AddOptionalStringProp(props, element, "backend-id", "BackendId");
        context.AddOptionalBoolProp(props, element, "sf-resolve-condition", "SfResolveCondition");
        context.AddOptionalStringProp(props, element, "sf-service-instance-name", "SfServiceInstanceName");
        context.AddOptionalStringProp(props, element, "sf-partition-key", "SfPartitionKey");
        context.AddOptionalStringProp(props, element, "sf-listener-name", "SfListenerName");
        context.AddOptionalStringProp(props, element, "dapr-app-id", "DaprAppId");
        context.AddOptionalStringProp(props, element, "dapr-method", "DaprMethod");
        context.AddOptionalStringProp(props, element, "dapr-namespace", "DaprNamespace");
        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "SetBackendService", "SetBackendServiceConfig", props);
    }
}
