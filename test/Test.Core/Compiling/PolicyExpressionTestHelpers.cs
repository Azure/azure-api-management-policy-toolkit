// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

// Compiles policy documents around expression helper members, for the expression compiler tests.
internal static class PolicyExpressionTestHelpers
{
    public static IDocumentCompilationResult CompileCondition(string members) =>
        $$"""
          [Document]
          public class PolicyDocument : IDocument
          {
              public void Inbound(IInboundContext context)
              {
                  if (Matches(context.ExpressionContext))
                  {
                      context.SetVariable("matched", "true");
                  }
              }

              {{members}}
          }
          """.CompileDocument();

    public static IDocumentCompilationResult CompileEvaluate(string members, params string[] separateDocuments) =>
        $$"""
          [Document]
          public class PolicyDocument : IDocument
          {
              public void Inbound(IInboundContext context)
              {
                  context.SetVariable("value", Evaluate(context.ExpressionContext));
              }

              {{members}}
          }
          """.CompileDocument(separateDocuments);

    public static string Value(IDocumentCompilationResult result) =>
        result.Document.Descendants("set-variable").Single().Attribute("value")!.Value;
}