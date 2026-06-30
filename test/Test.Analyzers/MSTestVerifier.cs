// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;

using Microsoft.CodeAnalysis.Testing;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers.Test;

public sealed class MSTestVerifier : IVerifier
{
    public void Empty<T>(string message, IEnumerable<T> collection)
    {
        if (collection.Any())
        {
            Assert.Fail(message);
        }
    }

    public void Equal<T>(T expected, T actual, string message)
    {
        Assert.AreEqual(expected, actual, message);
    }

    public void True(bool assert, string message)
    {
        Assert.IsTrue(assert, message);
    }

    public void False(bool assert, string message)
    {
        Assert.IsFalse(assert, message);
    }

    public void Fail(string message)
    {
        Assert.Fail(message);
    }

    public void LanguageIsSupported(string language)
    {
    }

    public void NotEmpty<T>(string message, IEnumerable<T> collection)
    {
        if (!collection.Any())
        {
            Assert.Fail(message);
        }
    }

    public void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual,
        IEqualityComparer<T> comparer, string message)
    {
        CollectionAssert.AreEqual(expected.ToList(), actual.ToList(), comparer as IComparer, message);
    }

    public IVerifier PushContext(string context)
    {
        return this;
    }
}
