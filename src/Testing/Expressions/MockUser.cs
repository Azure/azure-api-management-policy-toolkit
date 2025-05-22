// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockUser : IUser
{
    public string Email { get; set; } = "joe.doe@contoso.example";

    public string FirstName { get; set; } = "Joe";

    public List<MockGroup> MockGroups { get; set; } = new List<MockGroup>() { new MockGroup() };
    public IEnumerable<IGroup> Groups => MockGroups;

    public string Id { get; set; } = "joe-doe-contoso-example";

    public List<MockUserIdentity> MockUserIdentities { get; set; } =
        new List<MockUserIdentity>() { new MockUserIdentity() };

    public IEnumerable<IUserIdentity> Identities => MockUserIdentities;

    public string LastName { get; set; } = "Doe";

    public string Note { get; set; } = "Joe likes dogs and cats equally";

    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow.AddDays(-5);
}