# Gateway Emulator Policy Checklist

**Admitted snapshot:** `b4cf2e36b9218b9ed744ddcf25b12879df1bed7c`.
The complete `Test.Testing` project passed **3883/3883, with no skips** on this base.
This is not a final coverage or Full-gate result: semantic-cache/token-limit behavior and
structural-audit admission remain pending.

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
meaningfully tested on this base. **PENDING** means semantic behavior is not implemented
or tested; a registered no-op or callback smoke test is not completion.

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
| `AzureOpenAiSemanticCacheLookup` | I | `AzureOpenAiSemanticCacheLookupHandler` (no-op) | Dedicated callback smoke only; no cache semantics | **PENDING** |
| `AzureOpenAiSemanticCacheStore` | O | `AzureOpenAiSemanticCacheStoreHandler` (no-op) | Missing dedicated class | **PENDING** |
| `AzureOpenAiTokenLimit` | I | `AzureOpenAiTokenLimitHandler` (no-op) | Missing dedicated class | **PENDING** |
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
| `LlmSemanticCacheLookup` | I | `LlmSemanticCacheLookupHandler` (no-op) | Missing dedicated class | **PENDING** |
| `LlmSemanticCacheStore` | O | `LlmSemanticCacheStoreHandler` (no-op) | Missing dedicated class | **PENDING** |
| `LlmTokenLimit` | I | `LlmTokenLimitHandler` (no-op) | Missing dedicated class | **PENDING** |
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
| `Wait` | I, O, B, E | `WaitHandler` (fail-fast/explicit mock only) | Unsupported-parallel boundary + explicit mocks | ADMITTED |
| `XmlToJson` | I, O, B, E | `XmlToJsonHandler` | Behavioral | ADMITTED |
| `XslTransform` | I, O, E | Request / response | Behavioral | ADMITTED |

## Verified boundaries

These are tested contracts, not claims of complete APIM or live-cloud fidelity.

| Area | Boundary and accepted evidence |
|------|--------------------------------|
| Raw XML | `InlinePolicy(string policy)` cannot be interpreted by the strongly typed emulator. Default execution leaves state unchanged; explicit callbacks receive the raw string and can supply effects. `InlinePolicyTests` verifies unchanged state in all authored sections and inbound callback effects. |
| Legacy cross-domain policy | `CrossDomain(string policy)` validates well-formed XML with an unqualified `cross-domain-policy` root, then offers callbacks only. The user selected this contract because public APIM documentation does not establish the Flash/Silverlight routing trigger, status, headers, or termination behavior. No route-serving behavior is assumed. `CrossDomainTests` verifies validation, unchanged default state, and callback effects. |
| Parallel wait | The actual signature is `Wait(Action section, string? waitFor = null)`. The opaque action/choose logic does not expose safe immediate-child capture, isolated execution, or cancellation of losing work. Without a matching explicit mock, default/`all`/`any` throws `NotSupportedException` (wrapped in `PolicyException`) **before child effects**; sequential execution is not presented as parallel semantics. `WaitTests` verifies fail-fast behavior and explicit callbacks, including callbacks that deliberately invoke children synchronously. |
| Body transformations | `MockBody` stores text; byte views are UTF-8, not an arbitrary encoded/binary stream. XSLT rejects non-UTF-8 output; `JsonToXml` rejects non-UTF-8 declarations. XSLT and XML/JSON conversions update existing `Content-Length` from UTF-8 byte counts without adding an absent length; converters also normalize `Content-Type`. XSLT disables DTDs, external resources, scripts, and `document()` (`XslTransformTests`). XML/JSON conversion uses the projections asserted by `XmlToJsonTests` and `JsonToXmlTests`: mixed-text values survive, but their interleaving is not a lossless XML round trip; JavaScript-friendly output drops declarations, flattens attributes/prefixes, and rejects name collisions. |
| External services and telemetry | HTTP uses injected `IHttpClient` and evaluated `HttpTransportOptions`; the emulator does not create network clients or simulate wire-level streaming. Identity uses explicit offline token/key/trust or certificate evidence. Authorization, Dapr, Service Bus, schemas, and LLM evaluation/usage use registered test services or explicit callbacks, not live cloud calls. Telemetry records in `LoggerStore` or uses configured logger hooks; it is not automatically exported to Azure. The corresponding policy tests assert requests, outcomes, and failures. Default metric dimension `Backend ID` is unavailable: supply an explicit `Value` (`EmitMetricTests`, `LlmEmitTokenMetricTests`). |
| Schema/API validation | Supply `ApiValidationMetadata` and/or named `ContentValidationSchema`; no OpenAPI inference/import. JSON uses a strict supported subset: local references, exact decimal numbers, and at most 64 evaluation levels; unsupported keywords/formats, external references, tuple items, and reference assertion siblings fail explicitly. XSD must be self-contained, with DTD/external resolution disabled and UTF-8 payload modeling. Parameter schemas support primitive scalars/arrays, not complex object/style/explode serialization. `ValidateContentTests`, `ValidateParametersTests`, `ValidateHeadersTests`, and `ValidateStatusCodeTests` exercise these contracts. |
| OData | The built-in explicit model covers OData 4.0/4.01 entity collections, single scalar keys, JSON write schemas, and supported simple query options. Navigation, functions, batches, and expression queries require `IOdataRequestValidator`; EDM/CSDL is not imported or inferred. Unsupported advanced paths fail explicitly, as verified by `ValidateOdataRequestTests`. |
| LLM metrics | Both metric aliases record usage already available at the inbound invocation from JSON response usage or `ILlmTokenUsageProvider`. There is no deferred response/SSE collection or invented token estimation. Both aliases run the behavioral suite through `AzureOpenAiEmitTokenMetricTests : LlmEmitTokenMetricTests`. Semantic caching and token limiting remain **PENDING**, not covered by metric/content-safety completion. |

## Structural audit and validation gates

`test\Test.Testing\Emulator\EmulatorCoverageGateTests.cs` is **not admitted** and is absent
from this snapshot. The audit change at `3f345ab` is in the separate
`emulator/audit-preflight` worktree. Its preflight against `b4cf2e3` passes **6/7**:
only the dedicated executable-test-class assertion remains failing.
The five missing classes are `AzureOpenAiSemanticCacheStoreTests`,
`LlmSemanticCacheLookupTests`, `LlmSemanticCacheStoreTests`,
`AzureOpenAiTokenLimitTests`, and `LlmTokenLimitTests`.
`AzureOpenAiSemanticCacheLookupTests` already exists, but its callback-only smoke test
does not prove semantic-cache behavior. The structural audit checks inventory,
registrations, executable metadata, helper/fragment contracts, and the raw-XML exclusion;
it is not a replacement for behavioral tests.

The committed `emulator-gates.ps1` defines two separate gates. **Both** require a clean
worktree on the expected branch (default `emulator/admission-complete`).

| Gate | Required checks | Snapshot state |
|------|-----------------|----------------|
| Admission | Latest commit changes only the `-OwnedFiles` allowlist; `git diff --check HEAD^ HEAD`; `-TestFilter` selects every changed policy test class; targeted TRX has nonzero results and proves a passing test in each changed class; then the complete `Test.Testing` project runs. | Admitted groups passed; semantic-cache/token-limit groups remain pending. |
| Full | Requires the structural audit file above; runs the complete `Test.Testing` project, including that audit, then builds `apim-policy-toolkit.sln`. | Not passed on this base: the required audit file is not admitted, and its separate preflight still has the five-class gap. |

Run gates from the admission worktree, not an implementation/documentation branch.
Set `$ownedFiles` to every allowed changed path and `$testFilter` to the changed policy
test classes before the Admission invocation:

```powershell
.\emulator-gates.ps1 -Gate Admission -OwnedFiles $ownedFiles -TestFilter $testFilter
.\emulator-gates.ps1 -Gate Full
```

Use Windows paths and positional test-project syntax:
`dotnet test test\Test.Testing\Test.Testing.csproj` (local SDK 10 does not support
`dotnet test --project`). These gates do **not** run BVT/E2E tests; do not substitute
solution-wide `dotnet test`. A documentation-only checklist edit needs no test run.
