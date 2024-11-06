﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface BasicAuthCredentials
{
    public string Username { get; }
    public string Password { get; }
}