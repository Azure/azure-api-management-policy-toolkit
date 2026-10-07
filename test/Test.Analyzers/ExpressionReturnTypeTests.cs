// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers.Test;
using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Test.Analyzers;

[TestClass]
public class ExpressionReturnTypeTests
{
    public static Task VerifyAsync(string source, params DiagnosticResult[] diags)
    {
        var test = new BaseAnalyzerTest<ExpressionDefinitionAnalyzer>(source, diags);
        test.TestState.AdditionalReferences.Add(
            MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "Newtonsoft.Json.dll")));
        return test.RunAsync();
    }

    [TestMethod]
    [DataRow("object")]
    [DataRow("string")]
    [DataRow("bool")]
    [DataRow("char")]
    [DataRow("sbyte")]
    [DataRow("byte")]
    [DataRow("short")]
    [DataRow("ushort")]
    [DataRow("int")]
    [DataRow("uint")]
    [DataRow("long")]
    [DataRow("ulong")]
    [DataRow("float")]
    [DataRow("double")]
    [DataRow("decimal")]
    [DataRow("System.Guid")]
    [DataRow("System.DateTime")]
    [DataRow("System.TimeSpan")]
    [DataRow("System.Uri")]
    [DataRow("int?")]
    [DataRow("float?")]
    [DataRow("System.Guid?")]
    [DataRow("System.DateTime?")]
    [DataRow("System.Nullable<char>")]
    [DataRow("byte[]")]
    [DataRow("sbyte[]")]
    [DataRow("ProductState")]
    [DataRow("System.DayOfWeek")]
    [DataRow("System.DayOfWeek?")]
    [DataRow("SourceEnum")]
    [DataRow("Newtonsoft.Json.Linq.JToken")]
    [DataRow("Newtonsoft.Json.Linq.JObject")]
    [DataRow("Newtonsoft.Json.Linq.JArray")]
    [DataRow("Newtonsoft.Json.Linq.JValue")]
    [DataRow("Newtonsoft.Json.Linq.JContainer")]
    [DataRow("Newtonsoft.Json.Linq.JProperty")]
    [DataRow("Newtonsoft.Json.Linq.JConstructor")]
    [DataRow("Newtonsoft.Json.Linq.JRaw")]
    public async Task ShouldAllowReturnType(string type)
    {
        await VerifyAsync(
            $$"""
              enum SourceEnum { A }

              class Test
              {
                  [Expression]
                  {{type}} Method(IExpressionContext context) => default;
              }
              """
        );
    }

    [TestMethod]
    public async Task ShouldAllowNullableReferenceReturnType()
    {
        await VerifyAsync(
            """
            #nullable enable
            class Test
            {
                [Expression]
                string? Method(IExpressionContext context) => null;
            }
            """
        );
    }

    [TestMethod]
    [DataRow("System.IO.Stream", "System.IO.Stream")]
    [DataRow("System.Collections.Generic.List<string>", "System.Collections.Generic.List<System.String>")]
    [DataRow("System.IO.Stream[]", "System.IO.Stream[]")]
    [DataRow("string[]", "System.String[]")]
    [DataRow("byte?[]", "System.Byte?[]")]
    [DataRow("System.Collections.ObjectModel.ReadOnlyCollection<string>", "System.Collections.ObjectModel.ReadOnlyCollection<System.String>")]
    [DataRow("string[,]", "System.String[,]")]
    [DataRow("string[][]", "System.String[][]")]
    [DataRow("System.Threading.Tasks.Task<string>", "System.Threading.Tasks.Task<System.String>")]
    public async Task ShouldReportReturnType(string type, string reported)
    {
        await VerifyAsync(
            $$"""
              class Test
              {
                  [Expression]
                  {|#0:{{type}}|} Method(IExpressionContext context) => default;
              }
              """,
            DiagnosticResult
                .CompilerError(Rules.Expression.ReturnTypeNotAllowed.Id)
                .WithLocation(0)
                .WithArguments(reported)
        );
    }
}
