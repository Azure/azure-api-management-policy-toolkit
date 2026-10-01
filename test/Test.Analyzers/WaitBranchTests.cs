// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers.Test;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Test.Analyzers;

[TestClass]
public class WaitBranchTests
{
    private static Task VerifyAsync(string source, params DiagnosticResult[] diagnostics) =>
        new BaseAnalyzerTest<WaitBranchAnalyzer>(source, diagnostics).RunAsync();

    [TestMethod]
    [DataRow("IInboundContext", "Inbound")]
    [DataRow("IBackendContext", "Backend")]
    [DataRow("IOutboundContext", "Outbound")]
    [DataRow("IOnErrorContext", "OnError")]
    [DataRow("IFragmentContext", "Fragment")]
    public async Task AcceptsTypedBranchesInEverySection(string contextType, string section)
    {
        await VerifyAsync(
            $$"""
              public class Policy
              {
                  public void {{section}}({{contextType}} context)
                  {
                      context.Wait("all", child =>
                          child.WithId("request").SendRequest(
                              new SendRequestConfig { ResponseVariableName = "result" }));
                  }
              }
              """);
    }

    [TestMethod]
    public async Task AcceptsDirectPoliciesAndOneChooseBranch()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait("all",
                        child => child.SendRequest(new SendRequestConfig { ResponseVariableName = "first" }),
                        child =>
                        {
                            if (UseCache(child.ExpressionContext))
                            {
                                child.CacheLookupValue(new CacheLookupValueConfig { Key = "key", VariableName = "value" });
                                child.SendRequest(new SendRequestConfig { ResponseVariableName = "second" });
                            }
                            else if (UseCache(child.ExpressionContext))
                            {
                                child.CacheLookupValue(new CacheLookupValueConfig { Key = "other", VariableName = "value" });
                            }
                            else
                            {
                                child.SendRequest(new SendRequestConfig { ResponseVariableName = "backup" });
                            }
                        });
                }

                private static bool UseCache(IExpressionContext context) => context.Variables.ContainsKey("use-cache");
            }
            """);
    }

    [TestMethod]
    public async Task AcceptsCompileTimeNamesOfOuterContexts()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", child =>
                        child.WithId(nameof(context)).SendRequest(
                            new SendRequestConfig { ResponseVariableName = "response" }));
                }
            }
            """);
    }

    [TestMethod]
    public async Task AcceptsBranchExpressionContextAfterWithId()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", branch =>
                    {
                        if (ShouldRun(branch.WithId("metadata").ExpressionContext))
                        {
                            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "result" });
                        }
                    });
                }

                private static bool ShouldRun(IExpressionContext context) => true;
            }
            """);
    }

    [TestMethod]
    public async Task RejectsMultipleTopLevelPoliciesInOneBranch()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", {|#0:child =>
                    {
                        child.SendRequest(new SendRequestConfig { ResponseVariableName = "first" });
                        child.SendRequest(new SendRequestConfig { ResponseVariableName = "second" });
                    }|});
                }
            }
            """,
            DiagnosticResult.CompilerError(Rules.WaitBranch.InvalidChild.Id).WithLocation(0));
    }

    [TestMethod]
    public async Task RejectsUnsupportedImmediatePolicy()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", {|#0:child => child.Base()|});
                }
            }
            """,
            DiagnosticResult.CompilerError(Rules.WaitBranch.InvalidChild.Id).WithLocation(0));
    }

    [TestMethod]
    public async Task RejectsEmptyWait()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    {|#0:context.Wait("all")|};
                }
            }
            """,
            DiagnosticResult.CompilerError(Rules.WaitBranch.InvalidChild.Id).WithLocation(0));
    }

    [TestMethod]
    public async Task RejectsOpaqueBranchArrays()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", {|#0:new System.Action<IInboundContext>[]
                    {
                        child => child.SendRequest(new SendRequestConfig { ResponseVariableName = "hidden" })
                    }|});
                }
            }
            """,
            DiagnosticResult.CompilerError(Rules.WaitBranch.InvalidChild.Id).WithLocation(0));
    }

    [TestMethod]
    public async Task RejectsCapturedOuterPolicyContext()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", child =>
                    {
                        if (ShouldRun(child.ExpressionContext))
                        {
                            {|#0:context|}.SendRequest(new SendRequestConfig { ResponseVariableName = "wrong" });
                        }
                    });
                }

                private static bool ShouldRun(IExpressionContext context) => true;
            }
            """,
            DiagnosticResult.CompilerError(Rules.WaitBranch.CapturedContext.Id).WithLocation(0));
    }

    [TestMethod]
    public async Task RejectsCapturedOuterConditionContext()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", child =>
                    {
                        if (ShouldRun({|#0:context|}.ExpressionContext))
                        {
                            child.SendRequest(new SendRequestConfig { ResponseVariableName = "wrong" });
                        }
                    });
                }

                private static bool ShouldRun(IExpressionContext context) => true;
            }
            """,
            DiagnosticResult.CompilerError(Rules.WaitBranch.CapturedContext.Id).WithLocation(0));
    }

    [TestMethod]
    public async Task RejectsCapturedBaseExpressionContext()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    IHaveExpressionContext outer = context;
                    context.Wait("all", child =>
                    {
                        if (ShouldRun({|#0:outer|}.ExpressionContext))
                        {
                            child.SendRequest(new SendRequestConfig { ResponseVariableName = "wrong" });
                        }
                    });
                }

                private static bool ShouldRun(IExpressionContext context) => true;
            }
            """,
            DiagnosticResult.CompilerError(Rules.WaitBranch.CapturedContext.Id).WithLocation(0));
    }

    [TestMethod]
    public async Task RejectsCapturedExpressionContextAlias()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                private readonly IExpressionContext outer;

                public Policy(IExpressionContext outer) => this.outer = outer;

                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", branch =>
                    {
                        if (ShouldRun(branch.ExpressionContext))
                        {
                            branch.CacheLookupValue(new CacheLookupValueConfig
                            {
                                Key = "cached", VariableName = "cached"
                            });
                            if (Missing({|#0:outer|}))
                            {
                                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "fallback" });
                            }
                        }
                    });
                }

                private static bool ShouldRun(IExpressionContext context) => true;
                private static bool Missing(IExpressionContext context) =>
                    !context.Variables.ContainsKey("cached");
            }
            """,
            DiagnosticResult.CompilerError(Rules.WaitBranch.CapturedContext.Id).WithLocation(0));
    }
}
