// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public class ResponseExampleStore
{
    private readonly Dictionary<string, Dictionary<string, List<ResponseExample>>> _responseExamples = new();

    public ResponseExample[] GetOrDefault(string apiId, string operationId)
    {
        if (this._responseExamples.TryGetValue(apiId, out var operations) &&
            operations.TryGetValue(operationId, out var operationExamples))
        {
            return operationExamples.ToArray();
        }

        return [];
    }

    public void Add(GatewayContext context, params ResponseExample[] examples) =>
        Add(context.Api.Id, context.Operation.Id, examples);

    public void Add(string apiId, string operationId, params ResponseExample[] examples)
    {
        if (!this._responseExamples.TryGetValue(apiId, out var operations))
        {
            operations = new Dictionary<string, List<ResponseExample>>();
            this._responseExamples[apiId] = operations;
        }

        if (!operations.TryGetValue(operationId, out var operationExamples))
        {
            operationExamples = [];
            operations[operationId] = operationExamples;
        }

        operationExamples.AddRange(examples);
    }
}