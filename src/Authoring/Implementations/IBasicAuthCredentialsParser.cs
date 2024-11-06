﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Azure.ApiManagement.PolicyToolkit.Authoring.Implementations;

public interface IBasicAuthCredentialsParser
{
    BasicAuthCredentials? Parse(string? value);
}