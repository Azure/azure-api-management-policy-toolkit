// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

public enum DocumentScope
{
    Any = 0, Global, Workspace, Product, Api, Operation
}

public enum DocumentType
{
    Policy = 0, Fragment
}