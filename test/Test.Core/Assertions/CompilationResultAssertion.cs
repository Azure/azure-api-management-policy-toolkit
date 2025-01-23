// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Compiling;

using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Azure.ApiManagement.PolicyToolkit.Assertions;

public class CompilationResultAssertion : ObjectAssertions<ICompilationResult, CompilationResultAssertion>
{
    public CompilationResultAssertion(ICompilationResult value) : base(value)
    {
    }

    public AndConstraint<CompilationResultAssertion> BeSuccessful(string because = "", params object[] becauseArgs)
    {
        using var scope = new AssertionScope();
        scope.BecauseOf(because, becauseArgs);
        this.NotBeNull();
        Subject.Diagnostics.Should().BeEmpty();
        Subject.Document.Should().NotBeNull();
        return new AndConstraint<CompilationResultAssertion>(this);
    }

    public AndConstraint<CompilationResultAssertion> DocumentEquivalentTo(string expectedXml, string because = "", params object[] becauseArgs)
    {
        Subject.Document.Should().BeEquivalentTo(expectedXml, because, becauseArgs);
        return new AndConstraint<CompilationResultAssertion>(this);
    }
}