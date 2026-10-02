// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

public class FinishSectionProcessingException : Exception
{
    // Resolve this once at the originating policy, not again while unwinding wrappers.
    internal bool? TerminatesPipeline { get; set; }
}