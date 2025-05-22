// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class ForwardRequestTests
{
    class SimpleForwardRequest : IDocument
    {
        public void Backend(IBackendContext context)
        {
            context.ForwardRequest();
        }
    }

    [TestMethod]
    public void ForwardRequest_Callback()
    {
        var test = new SimpleForwardRequest().AsTestDocument();
        var executedCallback = false;
        test.SetupBackend().ForwardRequest().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunBackend();

        executedCallback.Should().BeTrue();
    }
}