# Gateway Emulator Policy Implementation Checklist

Track progress of emulator policy handler implementation. Each policy needs a handler in
`src/Testing/Emulator/Policies/` and tests in `test/Test.Testing/Emulator/Policies/`.

## Status Legend

- ⬜ Not started
- 🟡 Tests written, handler not implemented
- ✅ Done (handler + tests)

---

## Missing Handlers (no handler file exists)

| Status | Policy | Sections | Config | Mocking Strategy | Branch |
|--------|--------|----------|--------|-----------------|--------|
| ⬜ | AzureOpenAiTokenLimit | Inbound | AzureOpenAiTokenLimitConfig | No-op + callbacks | `emulator/azure-openai-token-limit` |
| ⬜ | CrossDomain | Inbound | None (no-arg) | No-op + callbacks | `emulator/cross-domain` |
| ⬜ | FindAndReplace | Inbound, Outbound, Backend, OnError | 2 strings (from, to) | Context mutation | `emulator/find-and-replace` |
| ⬜ | GetAuthorizationContext | Inbound, Outbound, Backend | GetAuthorizationContextConfig | External service mock | `emulator/get-authorization-context` |
| ⬜ | IncludeFragment | All sections | string (fragment-id) | No-op + callbacks | `emulator/include-fragment` |
| ⬜ | InvokeDaprBinding | Inbound, Outbound, OnError | InvokeDaprBindingConfig | External service mock | `emulator/invoke-dapr-binding` |
| ⬜ | LimitConcurrency | All sections | LimitConcurrencyConfig | No-op + callbacks | `emulator/limit-concurrency` |
| ⬜ | LlmContentSafety | Inbound | LlmContentSafetyConfig | No-op + callbacks | `emulator/llm-content-safety` |
| ⬜ | LlmTokenLimit | Inbound | LlmTokenLimitConfig | No-op + callbacks | `emulator/llm-token-limit` |
| ⬜ | Proxy | Inbound | ProxyConfig | Context mutation | `emulator/proxy` |
| ⬜ | PublishToDapr | Inbound, Outbound, OnError | PublishToDaprConfig | External service mock | `emulator/publish-to-dapr` |
| ⬜ | QuotaByKey | Inbound | QuotaByKeyConfig | No-op + callbacks | `emulator/quota-by-key` |
| ⬜ | RedirectContentUrls | Outbound | None (no-arg) | No-op + callbacks | `emulator/redirect-content-urls` |
| ⬜ | Retry | Inbound, Outbound, Backend, OnError | RetryConfig + delegate | Wrapper/flow control | `emulator/retry` |
| ⬜ | SendOneWayRequest | Inbound, Outbound, Backend, OnError | SendOneWayRequestConfig | External service mock | `emulator/send-one-way-request` |
| ⬜ | Trace | All sections | TraceConfig | Store interaction | `emulator/trace` |
| ⬜ | ValidateAzureAdToken | Inbound | ValidateAzureAdTokenConfig | Validation + short-circuit | `emulator/validate-azure-ad-token` |
| ⬜ | ValidateClientCertificate | Inbound | ValidateClientCertificateConfig | Validation + short-circuit | `emulator/validate-client-certificate` |
| ⬜ | ValidateContent | Inbound, Outbound, OnError | ValidateContentConfig | Validation + short-circuit | `emulator/validate-content` |
| ⬜ | ValidateHeaders | Inbound, Outbound, OnError | ValidateHeadersConfig | Validation + short-circuit | `emulator/validate-headers` |
| ⬜ | ValidateOdataRequest | Inbound | ValidateOdataRequestConfig | No-op + callbacks | `emulator/validate-odata-request` |
| ⬜ | ValidateParameters | Inbound | ValidateParametersConfig | Validation + short-circuit | `emulator/validate-parameters` |
| ⬜ | ValidateStatusCode | Outbound, OnError | ValidateStatusCodeConfig | Validation + short-circuit | `emulator/validate-status-code` |
| ⬜ | Wait | Inbound, Outbound, Backend | WaitConfig + delegates | Wrapper/flow control | `emulator/wait` |
| ⬜ | XmlToJson | Inbound, Outbound, Backend, OnError | XmlToJsonConfig | No-op + callbacks | `emulator/xml-to-json` |
| ⬜ | XslTransform | Inbound, Outbound, Backend, OnError | XslTransformConfig | No-op + callbacks | `emulator/xsl-transform` |

## Stub Handlers (file exists but throws NotImplementedException)

| Status | Policy | Handler File | Mocking Strategy | Branch |
|--------|--------|-------------|-----------------|--------|
| ⬜ | CacheLookup | CacheLookupHandler.cs | Store interaction | `emulator/cache-lookup` |
| ⬜ | CacheStore | CacheStoreHandler.cs | Store interaction | `emulator/cache-store` |
| ⬜ | Cors | CorsHandler.cs | Context mutation | `emulator/cors` |
| ⬜ | EmitMetric | EmitMetricHandler.cs | No-op + callbacks | `emulator/emit-metric` |
| ⬜ | ForwardRequest | ForwardRequestHandler.cs | External service mock | `emulator/forward-request` |
| ⬜ | JsonP | JsonPHandler.cs | Context mutation | `emulator/jsonp` |
| ⬜ | JsonToXml | JsonToXmlHandle.cs | No-op + callbacks | `emulator/json-to-xml` |
| ⬜ | LlmEmitTokenMetric | LlmEmitTokenMetricHandler.cs | No-op + callbacks | `emulator/llm-emit-token-metric` |
| ⬜ | LlmSemanticCacheLookup | LlmSemanticCacheLookupHandler.cs | No-op + callbacks | `emulator/llm-semantic-cache-lookup` |
| ⬜ | LlmSemanticCacheStore | LlmSemanticCacheStoreHandler.cs | No-op + callbacks | `emulator/llm-semantic-cache-store` |
| ⬜ | Quota | QuotaHandler.cs | No-op + callbacks | `emulator/quota` |
| ✅ | RateLimit | RateLimitHandler.cs | No-op + callbacks | `emulator/rate-limit` |
| ⬜ | RateLimitByKey | RateLimitByKeyHandler.cs | No-op + callbacks | `emulator/rate-limit-by-key` |
| ✅ | RewriteUri | RewriteUriHandler.cs | Context mutation | `emulator/rewrite-uri` |
| ⬜ | SendRequest | SendRequestHandler.cs | External service mock | `emulator/send-request` |
| ⬜ | SetBackendService | SetBackendServiceHandler.cs | Context mutation | `emulator/set-backend-service` |
| ⬜ | ValidateJwt | ValidateJwtHandler.cs | Validation + short-circuit | `emulator/validate-jwt` |

## Existing Handlers Missing Tests Only

| Status | Policy | Handler File | Branch |
|--------|--------|-------------|--------|
| ✅ | InlinePolicy | InlinePolicyHandler.cs | `emulator/tests-inline-policy` |
| ✅ | RemoveHeader | RemoveHeaderHandler.cs | `emulator/tests-remove-header` |
| ✅ | RemoveQueryParameter | RemoveQueryParameterHandler.cs | `emulator/tests-remove-query-parameter` |
| ✅ | ReturnResponse | ReturnResponseHandler.cs | `emulator/tests-return-response` |
| ✅ | SetBody | SetBodyHandler.cs | `emulator/tests-set-body` |
| ⬜ | SetHeaderIfNotExist | SetHeaderIfNotExistHandler.cs | `emulator/tests-set-header-if-not-exist` |
| ✅ | SetMethod | SetMethodHandler.cs | `emulator/tests-set-method` |
| ⬜ | SetQueryParameter | SetQueryParameterHandler.cs | `emulator/tests-set-query-parameter` |
| ⬜ | SetQueryParameterIfNotExist | SetQueryParameterIfNotExistHandler.cs | `emulator/tests-set-query-parameter-if-not-exist` |
| ✅ | SetVariable | SetVariableHandler.cs | `emulator/tests-set-variable` |
