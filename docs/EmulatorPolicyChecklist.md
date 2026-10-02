# Gateway Emulator Policy Checklist

**Verified merged-code snapshot:** `f62750a874f513cb4d1542ec560da9323c512e20`
on gatekeeper branch `emulator/admission-complete`.
All code packets, fixes, the structural audit, and current `main` are integrated.
The **Full** gate passed `Test.Testing` **5557/5557 on .NET 8**,
**5557/5557 on .NET 9**, and **5570/5570 on .NET 10**, with **0 skipped**;
the entire solution built with **0 errors / 0 warnings**.

All five baseline integrated review passes are complete: correctness, test quality,
consistency/style, meaningful duplication, and integration safety. Independent typed
`Wait` reviews verified their actionable findings fixed; **zero remain open**.
This identifies the verified post-merge code snapshot, not a documentation
commit, and does not claim unrestricted APIM parity.

## Inventory and status

The five authoring interfaces in `src\Authoring` expose **74 distinct policy methods** and
**190 pipeline policy/section pairs**. `WithId(string)` is a chaining/compile-time metadata
helper, not a policy; it is handled directly by the proxy and has separate `WithIdTests`.
Property accessors are also excluded from the policy count.

Sections: **I** = inbound (67 pairs), **O** = outbound (48), **B** = backend (34),
**E** = on-error (41). `IFragmentContext` exposes 73 policy methods, with **no `Base`**.
Fragments reuse the caller's section name and actual handler instances; they have no
independent handler registration and do not widen a policy's allowed sections.

Runtime bindings were checked through `SectionContextProxy.DiscoverHandlers`: concrete
`IPolicyHandler` types in the policy namespace, `[Section]` attributes, and instance
`PolicyName` values, including inherited implementations. Every authored pipeline pair
has one registered handler, but registration alone does not establish implemented behavior.

**ADMITTED** means behavior, or the explicitly bounded contract below, is implemented and
meaningfully tested on the gatekeeper snapshot. **ADMITTED (limited)** calls out the
semantic-cache/token-limit restrictions below. Neither state implies full cloud/APIM
parity; a registered no-op or callback smoke test alone is not completion.

In the table, **Behavioral** refers to the executable `<Policy>Tests` class in
`test\Test.Testing\Emulator\Policies`, with shared/exceptional evidence named explicitly.
For **Request / response** bindings, the concrete classes are `<Policy>RequestHandler`
(I/B) and `<Policy>ResponseHandler` (O/E), restricted to the sections shown.
`InvokeDarpBinding`, `PublishToDarp`, and `JsonToXmlHandle` retain their actual API/type spellings.

| Policy | Sections | Runtime handler(s) | Executable test evidence | State |
|--------|----------|--------------------|--------------------------|-------|
| `AppendHeader` | I, O, B, E | Request / response | Behavioral | ADMITTED |
| `AppendQueryParameter` | I, O, B, E | `AppendQueryParameterHandler` | Behavioral | ADMITTED |
| `AuthenticationBasic` | I | `AuthenticationBasicHandler` | Behavioral | ADMITTED |
| `AuthenticationCertificate` | I | `AuthenticationCertificateHandler` | Behavioral | ADMITTED |
| `AuthenticationManagedIdentity` | I, O | `AuthenticationManagedIdentityHandler` | `AuthenticationManagedIdentityHandlerTests` | ADMITTED |
| `AzureOpenAiEmitTokenMetric` | I | `AzureOpenAiEmitTokenMetricHandler` | Inherits behavioral `LlmEmitTokenMetricTests`; dedicated callback test | ADMITTED |
| `AzureOpenAiSemanticCacheLookup` | I | `AzureOpenAiSemanticCacheLookupHandler` | Behavioral + cross-alias round trips | **ADMITTED (limited)** |
| `AzureOpenAiSemanticCacheStore` | O | `AzureOpenAiSemanticCacheStoreHandler` | Behavioral + cross-alias round trips | **ADMITTED (limited)** |
| `AzureOpenAiTokenLimit` | I | `AzureOpenAiTokenLimitHandler` | Inherits behavioral `LlmTokenLimitTests` | **ADMITTED (limited)** |
| `Base` | I, O, B, E | `BaseHandler` | Behavioral | ADMITTED |
| `CacheLookup` | I | `CacheLookupHandler` | Behavioral | ADMITTED |
| `CacheLookupValue` | I, O, B, E | `CacheLookupValueHandler` | Behavioral + `CacheFamilyTests` | ADMITTED |
| `CacheRemoveValue` | I, O, B, E | `CacheRemoveValueHandler` | Dedicated callbacks; behavior in `CacheFamilyTests` | ADMITTED |
| `CacheStore` | O | `CacheStoreHandler` | Behavioral | ADMITTED |
| `CacheStoreValue` | I, O, B, E | `CacheStoreValueHandler` | Dedicated callbacks; behavior in `CacheFamilyTests` | ADMITTED |
| `CacheValue` | I, O, B, E | `CacheValueHandler` | Behavioral + `CacheValueBehaviorTests`, `CacheFamilyTests` | ADMITTED |
| `CheckHeader` | I | `CheckHeaderHandler` | Behavioral | ADMITTED |
| `Cors` | I | `CorsHandler` | Behavioral | ADMITTED |
| `CrossDomain` | I | `CrossDomainHandler` (XML validation/callback only) | XML-validation boundary + callbacks | ADMITTED |
| `EmitMetric` | I, O, E | `EmitMetricHandler` | Behavioral | ADMITTED |
| `FindAndReplace` | I, O, B, E | Request / response | Behavioral | ADMITTED |
| `ForwardRequest` | B | `ForwardRequestHandler` | Behavioral | ADMITTED |
| `GetAuthorizationContext` | I, O, B | `GetAuthorizationContextHandler` | Behavioral | ADMITTED |
| `IncludeFragment` | I, O, B, E | `IncludeFragmentHandler` | Behavioral | ADMITTED |
| `InlinePolicy` | I, O, B, E | `InlinePolicyHandler` (callback only) | Raw-XML exclusion + callbacks | ADMITTED |
| `InvokeDarpBinding` | I, O, E | `InvokeDarpBindingHandler` | Behavioral | ADMITTED |
| `InvokeRequest` | I, O, B, E | `InvokeRequestHandler` | Behavioral | ADMITTED |
| `IpFilter` | I | `IpFilterHandler` | Behavioral | ADMITTED |
| `JsonP` | O | `JsonPHandler` | Behavioral | ADMITTED |
| `JsonToXml` | I, O, B, E | `JsonToXmlHandle` | Behavioral | ADMITTED |
| `LimitConcurrency` | I, O, B, E | `LimitConcurrencyHandler` | Behavioral | ADMITTED |
| `LlmContentSafety` | I | `LlmContentSafetyHandler` | Behavioral | ADMITTED |
| `LlmEmitTokenMetric` | I | `LlmEmitTokenMetricHandler` | Behavioral | ADMITTED |
| `LlmSemanticCacheLookup` | I | `LlmSemanticCacheLookupHandler` | Behavioral: text envelopes, hits/misses, partitions | **ADMITTED (limited)** |
| `LlmSemanticCacheStore` | O | `LlmSemanticCacheStoreHandler` | Behavioral: HTTP 200 snapshots, TTL, failures | **ADMITTED (limited)** |
| `LlmTokenLimit` | I | `LlmTokenLimitHandler` | Behavioral + backend/final-response integration | **ADMITTED (limited)** |
| `LogToEventHub` | I, O, B, E | `LogToEventHubHandler` | Behavioral | ADMITTED |
| `MockResponse` | I, O, E | `MockResponseHandler` | Behavioral | ADMITTED |
| `Proxy` | I | `ProxyHandler` | Behavioral | ADMITTED |
| `PublishToDarp` | I, O, E | `PublishToDarpHandler` | Behavioral | ADMITTED |
| `Quota` | I | `QuotaHandler` | Behavioral | ADMITTED |
| `QuotaByKey` | I | `QuotaByKeyHandler` | Behavioral | ADMITTED |
| `RateLimit` | I | `RateLimitHandler` | Behavioral | ADMITTED |
| `RateLimitByKey` | I | `RateLimitByKeyHandler` | Behavioral | ADMITTED |
| `RedirectContentUrls` | I, O | `RedirectContentUrlsHandler` | Behavioral | ADMITTED |
| `RemoveHeader` | I, O, B, E | Request / response | Behavioral | ADMITTED |
| `RemoveQueryParameter` | I, O, B, E | `RemoveQueryParameterHandler` | Behavioral | ADMITTED |
| `Retry` | I, O, B, E | `RetryHandler` | Behavioral | ADMITTED |
| `ReturnResponse` | I, O, B, E | `ReturnResponseHandler` | Behavioral | ADMITTED |
| `RewriteUri` | I | `RewriteUriHandler` | Behavioral | ADMITTED |
| `SendOneWayRequest` | I, O, B, E | `SendOneWayRequestHandler` | Behavioral | ADMITTED |
| `SendRequest` | I, O, B, E | `SendRequestHandler` | Behavioral | ADMITTED |
| `SendServiceBusMessage` | I, O, E | `SendServiceBusMessageHandler` | Behavioral | ADMITTED |
| `SetBackendService` | I, O, B, E | `SetBackendServiceHandler` | Behavioral | ADMITTED |
| `SetBody` | I, O, B, E | Request / response | Behavioral | ADMITTED |
| `SetHeader` | I, O, B, E | Request / response | Behavioral | ADMITTED |
| `SetHeaderIfNotExist` | I, O, B, E | Request / response | Behavioral | ADMITTED |
| `SetMethod` | I, O, B, E | `SetMethodHandler` | Behavioral | ADMITTED |
| `SetQueryParameter` | I, O, B, E | `SetQueryParameterHandler` | Behavioral | ADMITTED |
| `SetQueryParameterIfNotExist` | I, O, B, E | `SetQueryParameterIfNotExistHandler` | Behavioral | ADMITTED |
| `SetStatus` | I, O, B, E | `SetStatusHandler` | Behavioral | ADMITTED |
| `SetVariable` | I, O, B, E | `SetVariableHandler` | Behavioral | ADMITTED |
| `Trace` | I, O, B, E | `TraceHandler` | Behavioral | ADMITTED |
| `ValidateAzureAdToken` | I | `ValidateAzureAdTokenHandler` | Behavioral | ADMITTED |
| `ValidateClientCertificate` | I | `ValidateClientCertificateHandler` | Behavioral | ADMITTED |
| `ValidateContent` | I, O, E | `ValidateContentHandler` | Behavioral | ADMITTED |
| `ValidateHeaders` | O, E | `ValidateHeadersHandler` | Behavioral | ADMITTED |
| `ValidateJwt` | I | `ValidateJwtHandler` | Behavioral | ADMITTED |
| `ValidateOdataRequest` | I | `ValidateOdataRequestHandler` | Behavioral | ADMITTED |
| `ValidateParameters` | I | `ValidateParametersHandler` | Behavioral | ADMITTED |
| `ValidateStatusCode` | O, E | `ValidateStatusCodeHandler` | Behavioral | ADMITTED |
| `Wait` | I, O, B, E | `WaitHandler` (typed parallel branches; obsolete legacy mock) | Concurrency/cancellation, nested policy families, fragment and XML execution | ADMITTED |
| `XmlToJson` | I, O, B, E | `XmlToJsonHandler` | Behavioral | ADMITTED |
| `XslTransform` | I, O, E | Request / response | Behavioral | ADMITTED |

## Verified boundaries

These are tested contracts, not claims of complete APIM or live-cloud fidelity.

| Area | Boundary and accepted evidence |
|------|--------------------------------|
| Raw XML | `InlinePolicy(string policy)` cannot be interpreted by the strongly typed emulator. Default execution leaves state unchanged; explicit callbacks receive the raw string and can supply effects. `InlinePolicyTests` verifies unchanged state in all authored sections and inbound callback effects. |
| Legacy cross-domain policy | `CrossDomain(string policy)` validates well-formed XML with an unqualified `cross-domain-policy` root, then offers callbacks only. The user selected this contract because public APIM documentation does not establish the Flash/Silverlight routing trigger, status, headers, or termination behavior. No route-serving behavior is assumed. `CrossDomainTests` verifies validation, unchanged default state, and callback effects. |
| Parallel wait | New `Wait(string? waitFor, params Action<I{Section}Context>[] branches)` overloads give each action the corresponding inbound/backend/outbound/on-error or fragment context. Each action compiles to **one** direct `SendRequest`/`CacheLookupValue` or **one** `if`/`else if`/`else` chain (`choose`); inside a `choose`, all policies already supported by the emulator for that section can run sequentially. The compiler reports invalid shapes and captured outer expression/section contexts, the packaged analyzer reports APIM105/APIM106, and the decompiler emits typed branches. Branches execute concurrently with shallow-copied variable dictionaries and independent handler setups; registered stores and logical-request accounting remain shared. `all` waits for every branch and merges changed variable entries in authored order (not removals); `any` takes the **first completion, including errors**, cancels losers, and propagates its winner's variable entries. Already-published response/request and external side effects from a canceled branch are not undone. To avoid racing the public mock dictionaries, message changes are synchronized and published at policy boundaries rather than using the gateway's physically shared message references; callback object identity and intermediate visibility can therefore differ. Terminating policies end their child pipeline without terminating the parent; their response effects remain shared. Injected services, trace hooks, and callbacks must support concurrent calls; cancellation cannot forcefully stop arbitrary synchronous callback code, and mutable variable objects retain shared reference identity. `WaitBranches().WithCallback(...)` and `WaitFragmentBranches().WithCallback(...)` provide explicit overrides. The original `[Obsolete] Wait(Action section, string? waitFor = null)` remains available for compilation and explicit mocks, but still fails before child effects by default. `Wait` inside `Retry` is rejected. `WaitTests`, `WaitBranchTests`, and XML round-trip tests cover these boundaries. |
| Wait diagnostics | Invalid typed branches report compiler diagnostic **APIM2020**; `main` reserves APIM2012 for recursive expression helpers. The packaged analyzer continues to report APIM105/APIM106. Source-proven branch-local context aliases compile with the current semantic expression compiler; captured or unproven factories still fail. |
| Parallel wait header callbacks | Header policy handlers publish explicit writes and removals even when the new value matches a branch's prior snapshot. A raw callback that assigns the exact same existing header-array object cannot be distinguished from a no-op; use a header policy or assign a new array to publish an explicit write. `WaitTests.ReviewCorrections` covers ordered sibling writes, case variants, and callback replacements. |
| Body transformations | `MockBody` stores text; byte views are UTF-8, not an arbitrary encoded/binary stream. XSLT rejects non-UTF-8 output; `JsonToXml` rejects non-UTF-8 declarations. XSLT and XML/JSON conversions update existing `Content-Length` from UTF-8 byte counts without adding an absent length; converters also normalize `Content-Type`. XSLT disables DTDs, external resources, scripts, and `document()` (`XslTransformTests`). XML/JSON conversion uses the projections asserted by `XmlToJsonTests` and `JsonToXmlTests`: mixed-text values survive, but their interleaving is not a lossless XML round trip; JavaScript-friendly output drops declarations, flattens attributes/prefixes, and rejects name collisions. |
| External services and telemetry | HTTP uses injected `IHttpClient` and evaluated `HttpTransportOptions`; the emulator does not create network clients or simulate wire-level streaming. Identity uses explicit offline token/key/trust or certificate evidence. Authorization, Dapr, Service Bus, schemas, and LLM embeddings/tokenization/evaluation/usage use registered test services or explicit callbacks, not live cloud calls. Telemetry records in `LoggerStore` or uses configured logger hooks; it is not automatically exported to Azure. The corresponding policy tests assert requests, outcomes, and failures. Default metric dimension `Backend ID` is unavailable: supply an explicit `Value` (`EmitMetricTests`, `LlmEmitTokenMetricTests`). |
| Schema/API validation | Supply `ApiValidationMetadata` and/or named `ContentValidationSchema`; no OpenAPI inference/import. JSON uses a strict supported subset: local references, exact decimal numbers, and at most 64 evaluation levels; unsupported keywords/formats, external references, tuple items, and reference assertion siblings fail explicitly. XSD must be self-contained, with DTD/external resolution disabled and UTF-8 payload modeling. Parameter schemas support primitive scalars/arrays, not complex object/style/explode serialization. `ValidateContentTests`, `ValidateParametersTests`, `ValidateHeadersTests`, and `ValidateStatusCodeTests` exercise these contracts. |
| OData | The built-in explicit model covers OData 4.0/4.01 entity collections, single scalar keys, JSON write schemas, and supported simple query options. Navigation, functions, batches, and expression queries require `IOdataRequestValidator`; EDM/CSDL is not imported or inferred. Unsupported advanced paths fail explicitly, as verified by `ValidateOdataRequestTests`. |
| LLM metrics | Both metric aliases record usage already available at the inbound invocation from JSON response usage or `ILlmTokenUsageProvider`. There is no deferred response/SSE collection or invented token estimation. Both aliases run the behavioral suite through `AzureOpenAiEmitTokenMetricTests : LlmEmitTokenMetricTests`; semantic caching and token limiting have separate behavioral suites. |
| Semantic cache | Both lookup/store families support text-only Chat Completions/Responses, Anthropic Messages, and Vertex `contents` envelopes. They require injected `ISemanticCacheEmbeddingProvider` (only `system-assigned` authentication is modeled; no token acquisition) and a configured external `ICache` fixture. Default local cosine distance is not Redis/model parity. `CacheId` requires an exact named registration without fallback; API, operation, endpoint, request options, backend, and ordered `VaryBy` isolate partitions. Enabled tools/tool-call history or responses, audio/multimodal content, and streaming requests/responses/RPCs fail explicitly. Stores require the prior inbound lookup and cache **HTTP 200 only**, with TTL in seconds. All four dedicated semantic-cache test classes verify these bounded behaviors and cross-alias cache sharing. |
| Token limits | Both aliases enforce a rolling **60-second** rate (not the v2 token bucket) and fixed UTC Hourly/Daily/Weekly/Monthly/Yearly quotas, independently or together; weekly windows start Monday. Rate rejection is 429; quota rejection is 403. Enabling `EstimatePromptToken` requires injected `ITokenLimitPromptEstimator`, not a character-count heuristic. Actual backend prompt + completion usage is observed before outbound and settled once per key at final request completion, from JSON usage or `ILlmTokenUsageProvider`; missing usage is not invented. `RunAll` / successful outer `RunRequest` completes settlement; standalone sections need an explicit completion boundary. Share `TokenLimitCounterStore` and a consistent clock for shared counters. Streaming (`stream: true` / SSE) and estimated-image accounting fail explicitly; unestimated images require actual backend usage. `LlmTokenLimitTests` and inherited `AzureOpenAiTokenLimitTests` cover these contracts and coupled pipeline/HTTP behavior. |
| Generated response-header provenance | `PolicyResponseHeaderOverlay` tracks generated CORS/rate/token outputs for the owning context, `RequestId`, and response object. Native `ForwardRequest` restores registered outputs after backend copying and before token-usage observation; arbitrary seeded/mock headers are not retained merely because their names resemble policy outputs. Replay respects subsequent overrides/removals of registered outputs, with case-insensitive replacement and preserved injected dictionary/comparer. Valid native CORS reconfiguration/no-match retires only its own earlier generated outputs. New request IDs or response-object replacements retire old provenance. `CorsTests`, `ForwardRequestTests`, `SetHeaderTests`, and `RemoveHeaderTests` verify these boundaries. |
| Credentialed CORS wildcard exposure | With `AllowCredentials = true` and `ExposeHeaders` containing `*`, standalone inbound execution preserves immediate expansion over current headers. Native forwarding refreshes the wildcard portion from actual backend/policy headers while retaining explicitly configured exposure names; successful outer `RunAll` / `RunRequest` completion refreshes it after final response processing and limiter settlement. Wildcard enumeration excludes CORS control headers; native expansion also filters `Set-Cookie` and `Set-Cookie2`. Explicit exposure overrides/removals win; standalone sections do not imply request completion. Execution/settlement failures defer final refresh until successful recovery, and terminal/replacement responses are not taken over by an old resolver. `ForwardRequestTests`, `TestDocumentTests`, and `PolicyPipelineTests` cover flat/nested execution and completion-only recovery. |
| Explicit callback ownership | A selected `ForwardRequest` callback bypasses native transport/header replay and retires deferred wildcard provenance before execution, including when its failure is later handled. CORS-looking names copied into a mock response do not authorize deferred exposure; successful forwarding callbacks still use backend-usage observation. Callbacks overriding response `SetHeader` / `RemoveHeader` do not implicitly perform default normalization or cancellation: a no-op callback does not cancel deferred exposure merely by invocation. The corresponding policy tests plus `TestDocumentTests` and `PolicyPipelineTests` verify that old deferred resolvers do not take ownership of callback-owned, terminal, or replacement responses. |

## Out-of-scope compilation integration caveat

The pre-existing `src\Core\Compiling\Policy\TokenLimitCompiler.cs` is unchanged by this
emulator task. It emits singular `estimate-prompt-token` and rejects configurations
containing both rate and quota. The
[current APIM token-limit documentation](https://learn.microsoft.com/en-us/azure/api-management/llm-token-limit-policy)
specifies plural `estimate-prompt-tokens` and permits rate, quota, or both.
The emulator supports both together, but passing emulator tests does not establish
that this compiler's XML conforms to the current APIM contract. No compiler fix is included.

## Structural audit and validation gates

`test\Test.Testing\Emulator\EmulatorCoverageGateTests.cs` is admitted, and all **19/19**
structural audit tests pass on each target framework in the verified code snapshot.
All semantic-cache/token-limit rows have executable behavioral coverage, including
the inherited Azure OpenAI token-limit suite.
The structural audit checks inventory, registrations, executable metadata,
helper/fragment contracts, and the raw-XML exclusion; it is not a replacement for
behavioral tests or proof of unrestricted APIM parity.

The committed `emulator-gates.ps1` defines two separate gates. **Both** require a clean
worktree on the expected branch (default `emulator/admission-complete`).

| Gate | Required checks | Snapshot state |
|------|-----------------|----------------|
| Admission | Latest policy-packet commit changes only the `-OwnedFiles` allowlist; `git diff --check HEAD^ HEAD`; `-TestFilter` selects every changed policy test class, including partial `WaitTests.*.cs` files; targeted TRX has nonzero results and proves a passing test in each changed class; then the complete `Test.Testing` project runs. | **PASSED** on every typed `Wait` policy commit: 123 targeted / 5393 complete, 166 / 5448, 219 / 5501, 1 / 5502, and 287 / 5570 on the last runtime correction `b72392a`; 0 skipped throughout. |
| Full | Requires the structural audit file above; runs the complete `Test.Testing` project, including that audit, then builds `apim-policy-toolkit.sln`. | **PASSED** on merged-code snapshot `f62750a`: .NET 8 and 9 each 5557/5557, .NET 10 5570/5570, 0 skipped; 19/19 structural audit cases per framework, solution build 0 warnings / 0 errors. |

The compiler's typed `Wait` cases passed **152/152**; the complete compiler
suite passed **924/924**. The packaged analyzer suite passed **46/46**, including
all **32/32** typed `Wait` cases. Typed `Wait` plus real-policy XML round trips
passed **63/63**. The 13 compile-to-emulator cases needing the .NET 10-only Core
compiler run on .NET 10; the other emulator cases run on all three frameworks.

The local host has no .NET 9 runtime, so the .NET 9-compiled test binary was
executed using `DOTNET_ROLL_FORWARD=Major` and the .NET 10 runtime. CI provisions
the actual .NET 8, 9, and 10 runtimes; local roll-forward is not a substitute for
the .NET 9 CI run.

Run gates from the admission worktree, not an implementation/documentation branch.
Set `$ownedFiles` to every allowed changed path and `$testFilter` to the changed policy
test classes before a policy-packet Admission invocation. Audit-only/documentation-only
commits do not satisfy that gate's changed-policy-test-class requirement; check the
audit separately or rerun Full as needed:

```powershell
.\emulator-gates.ps1 -Gate Admission -OwnedFiles $ownedFiles -TestFilter $testFilter
dotnet test test\Test.Testing\Test.Testing.csproj --filter "FullyQualifiedName~EmulatorCoverageGateTests" --no-logo --verbosity quiet
.\emulator-gates.ps1 -Gate Full
```

Rerun Full after the documentation-only commits are admitted. That final check
confirms the documentation change without altering the verified code snapshot
or reopening the completed code reviews.

Use Windows paths and positional test-project syntax:
`dotnet test test\Test.Testing\Test.Testing.csproj` (local SDK 10 does not support
`dotnet test --project`). These gates do **not** run BVT/E2E tests; do not substitute
solution-wide `dotnet test`. A documentation-only checklist edit needs no test run.
