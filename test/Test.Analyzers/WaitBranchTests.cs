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

    [TestMethod]
    public async Task RejectsCapturedExpressionContextThroughHelper()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                private readonly IInboundContext outer;

                public Policy(IInboundContext outer) => this.outer = outer;

                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", branch =>
                    {
                        if (ShouldRun({|#0:GetOuter|}().ExpressionContext))
                        {
                            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "wrong" });
                        }
                    });
                }

                private IInboundContext GetOuter() => outer;
                private static bool ShouldRun(IExpressionContext context) => true;
            }
            """,
            DiagnosticResult.CompilerError(Rules.WaitBranch.CapturedContext.Id).WithLocation(0));
    }

    [TestMethod]
    public async Task RejectsCapturedExpressionContextFactoryResult()
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
                            if (Missing({|#0:GetOuter|}()))
                            {
                                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "fallback" });
                            }
                        }
                    });
                }

                private IExpressionContext GetOuter() => outer;
                private static bool ShouldRun(IExpressionContext context) => true;
                private static bool Missing(IExpressionContext context) =>
                    !context.Variables.ContainsKey("cached");
            }
            """,
            DiagnosticResult.CompilerError(Rules.WaitBranch.CapturedContext.Id).WithLocation(0));
    }

    [TestMethod]
    public async Task AcceptsProvenBranchContextFactories()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", branch =>
                    {
                        if (ShouldRun(Forward(FromSection(branch.WithId("metadata")))))
                        {
                            branch.SendRequest(new SendRequestConfig
                            {
                                ResponseVariableName = NameExp(Identity(context: branch.ExpressionContext))
                            });
                        }
                    });
                }

                private static bool ShouldRun(IExpressionContext context) => true;
                private static string NameExp(IExpressionContext context) => context.Request.Method;
                private static IExpressionContext Identity(IExpressionContext context) => context;
                private static IExpressionContext Forward(IExpressionContext context) => Identity(context);
                private static IExpressionContext FromSection(IInboundContext context) => context.ExpressionContext;
            }
            """);
    }

    [TestMethod]
    public async Task AcceptsProvenNestedWaitContextFactory()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", branch =>
                    {
                        if (ShouldRun(branch.ExpressionContext))
                        {
                            branch.Wait(null, inner => inner.SendRequest(new SendRequestConfig
                            {
                                ResponseVariableName = NameExp(Identity(inner.ExpressionContext))
                            }));
                        }
                    });
                }

                private static bool ShouldRun(IExpressionContext context) => true;
                private static string NameExp(IExpressionContext context) => context.Request.Method;
                private static IExpressionContext Identity(IExpressionContext context) => context;
            }
            """);
    }

    [TestMethod]
    public async Task AcceptsProvenContextHelperDeclaredInAnotherFile()
    {
        var test = new BaseAnalyzerTest<WaitBranchAnalyzer>(
            """
            public partial class Policy : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", branch =>
                    {
                        if (ShouldRun(Identity(branch.ExpressionContext)))
                        {
                            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "result" });
                        }
                    });
                }

                private static bool ShouldRun(IExpressionContext context) => true;
            }
            """);
        test.TestState.Sources.Add(
            """
            using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

            namespace Mielek.Test;

            public partial class Policy
            {
                private static IExpressionContext Identity(IExpressionContext context) => context;
            }
            """);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task AcceptsBranchLocalSectionHelperProjection()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", branch =>
                    {
                        if (ShouldRun(SectionIdentity(branch).ExpressionContext))
                        {
                            branch.CacheLookupValue(new CacheLookupValueConfig
                            {
                                Key = "key", VariableName = "cached"
                            });
                        }
                    });
                }

                private static bool ShouldRun(IExpressionContext context) => true;
                private static IInboundContext SectionIdentity(IInboundContext context) => context;
            }
            """);
    }

    [TestMethod]
    [DataRow("Identity(Identity(branch.ExpressionContext))")]
    [DataRow("Forward(Identity(branch.ExpressionContext))")]
    public async Task AcceptsFiniteNestedContextHelpers(string contextExpression)
    {
        await VerifyAsync(
            $$"""
              public class Policy : IDocument
              {
                  public void Inbound(IInboundContext context)
                  {
                      context.Wait("all", branch =>
                      {
                          if (ShouldRun({{contextExpression}}))
                          {
                              branch.CacheLookupValue(new CacheLookupValueConfig
                              {
                                  Key = "key", VariableName = "cached"
                              });
                          }
                      });
                  }

                  private static bool ShouldRun(IExpressionContext context) => true;
                  private static IExpressionContext Identity(IExpressionContext context) => context;
                  private static IExpressionContext Forward(IExpressionContext context) => Identity(context);
              }
              """);
    }

    [TestMethod]
    public async Task RejectsGenuinelyRecursiveContextHelper()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", branch =>
                    {
                        if (ShouldRun({|#0:ContextLoop|}(branch.ExpressionContext)))
                        {
                            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "wrong" });
                        }
                    });
                }

                private static bool ShouldRun(IExpressionContext context) => true;
                private static IExpressionContext ContextLoop(IExpressionContext context) => ContextLoop(context);
            }
            """,
            DiagnosticResult.CompilerError(Rules.WaitBranch.CapturedContext.Id).WithLocation(0));
    }

    [TestMethod]
    public async Task RejectsCapturedContextDelegateInvocation()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                private readonly System.Func<IExpressionContext> _factory;

                public Policy(IExpressionContext outer) => _factory = () => outer;

                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", branch =>
                    {
                        if (ShouldRun({|#0:_factory()|}))
                        {
                            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "wrong" });
                        }
                    });
                }

                private static bool ShouldRun(IExpressionContext context) => true;
            }
            """,
            DiagnosticResult.CompilerError(Rules.WaitBranch.CapturedContext.Id).WithLocation(0));
    }

    [TestMethod]
    public async Task RejectsUnprovenConditionalContextFactory()
    {
        await VerifyAsync(
            """
            public class Policy : IDocument
            {
                private readonly IExpressionContext _outer;

                public Policy(IExpressionContext outer) => _outer = outer;

                public void Inbound(IInboundContext context)
                {
                    context.Wait("all", branch =>
                    {
                        if (ShouldRun({|#0:ChooseContext|}(branch.ExpressionContext)))
                        {
                            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "wrong" });
                        }
                    });
                }

                private static bool ShouldRun(IExpressionContext context) => true;
                private IExpressionContext ChooseContext(IExpressionContext context) =>
                    context.Tracing ? context : _outer;
            }
            """,
            DiagnosticResult.CompilerError(Rules.WaitBranch.CapturedContext.Id).WithLocation(0));
    }
}
