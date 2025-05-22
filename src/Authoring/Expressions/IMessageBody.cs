// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface IMessageBody
{
    T As<T>(bool preserveContent = false);
    IDictionary<string, IList<string>> AsFormUrlEncodedContent(bool preserveContent = false);
}