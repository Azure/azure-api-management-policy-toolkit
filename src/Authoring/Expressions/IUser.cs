// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface IUser
{
    string Email { get; }
    string FirstName { get; }
    IEnumerable<IGroup> Groups { get; }
    string Id { get; }
    IEnumerable<IUserIdentity> Identities { get; }
    string LastName { get; }
    string Note { get; }
    DateTime RegistrationDate { get; }
}