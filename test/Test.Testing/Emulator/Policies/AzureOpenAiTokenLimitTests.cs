// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class AzureOpenAiTokenLimitTests : LlmTokenLimitTests
{
    protected override bool UseAzureOpenAi => true;
}