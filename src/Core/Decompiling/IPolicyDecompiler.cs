// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling;

public interface IPolicyDecompiler
{
    string PolicyName { get; }
    void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context);
}
