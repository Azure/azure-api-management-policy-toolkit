// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions.Execution;
using FluentAssertions.Primitives;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Assertions;

public class CompilationResultAssertion : ObjectAssertions<IDocumentCompilationResult, CompilationResultAssertion>
{
    public CompilationResultAssertion(IDocumentCompilationResult value) : base(value)
    {
    }

    public AndConstraint<CompilationResultAssertion> BeSuccessful(string because = "", params object[] becauseArgs)
    {
        using var scope = new AssertionScope();
        scope.BecauseOf(because, becauseArgs);
        this.NotBeNull();
        Subject.Errors.Should().BeEmpty();
        Subject.Document.Should().NotBeNull();
        return new AndConstraint<CompilationResultAssertion>(this);
    }

    public AndConstraint<CompilationResultAssertion> DocumentEquivalentTo(string expectedXml, string because = "",
        params object[] becauseArgs)
    {
        Subject.Document.Should().BeEquivalentTo(expectedXml, because, becauseArgs);
        return new AndConstraint<CompilationResultAssertion>(this);
    }
}