# Available Policies

This document lists policy elements that the toolkit's authoring and compiler support by mapping C# APIs to Azure API
Management policy elements. The list below was generated from the public authoring interfaces (
inbound/outbound/backend/on-error/fragment contexts) and reflects which policies the toolkit can compile into XML.

The toolkit also includes a **decompiler** that converts existing policy XML back to C# code, enabling round-trip
workflows: XML → C# → XML. Policies marked with ✅ support full round-trip (compiler + decompiler). Policies marked
with 🔧 support compilation only (no decompiler yet).

If a policy you need is missing you can either:

- Use `InlinePolicy(...)` to insert raw XML into the compiled document, or
- Open an issue / contribute the feature (learn more in [our contribution guide](../CONTRIBUTING.md)).

Notes:

- The C# compiler maps C# constructs to policy constructs. For example, if/else in C# is compiled to the `choose`
  policy (with `when` / `otherwise`).
- `InlinePolicy(string)` allows inserting arbitrary XML when a policy isn't implemented as a first-class API.
- The decompiler uses `InlinePolicy` as a fallback for any XML element it does not have a specific handler for.

## Implemented policies

- ✅ authentication-basic
- ✅ authentication-certificate
- ✅ authentication-managed-identity
- ✅ azure-openai-emit-token-metric
- ✅ azure-openai-semantic-cache-lookup
- ✅ azure-openai-semantic-cache-store
- ✅ azure-openai-token-limit
- ✅ base
- ✅ cache-lookup
- ✅ cache-lookup-value
- ✅ cache-remove-value
- ✅ cache-store
- ✅ cache-store-value
- ✅ cache-value
- ✅ check-header
- ✅ choose (implemented via C# if/else -> choose/when/otherwise)
- ✅ cors
- ✅ cross-domain
- ✅ emit-metric
- ✅ find-and-replace
- ✅ forward-request
- ✅ get-authorization-context
- ✅ include-fragment
- ✅ inline-policy (method to insert raw XML)
- ✅ invoke-dapr-binding (publish/send to Dapr bindings)
- ✅ ip-filter
- ✅ json-to-xml
- ✅ jsonp
- ✅ limit-concurrency
- ✅ llm-content-safety
- ✅ llm-emit-token-metric
- ✅ llm-semantic-cache-lookup
- ✅ llm-semantic-cache-store
- ✅ llm-token-limit
- ✅ log-to-eventhub
- ✅ mock-response
- ✅ proxy
- ✅ publish-to-dapr
- ✅ quota
- ✅ quota-by-key
- ✅ rate-limit
- ✅ rate-limit-by-key
- ✅ redirect-content-urls
- ✅ remove-header (via SetHeader/RemoveHeader APIs)
- ✅ remove-query-parameter (via SetQueryParameter/Remove APIs)
- ✅ return-response
- ✅ retry
- ✅ rewrite-uri
- ✅ send-one-way-request
- ✅ send-request
- ✅ send-service-bus-message
- ✅ set-backend-service (including Dapr attributes: dapr-app-id, dapr-method, dapr-namespace)
- ✅ set-body
- ✅ set-header
- ✅ set-header-if-not-exist
- ✅ set-method
- ✅ set-query-parameter
- ✅ set-query-parameter-if-not-exist
- ✅ set-status
- ✅ set-variable
- ✅ trace
- ✅ validate-azure-ad-token
- ✅ validate-client-certificate
- ✅ validate-content
- ✅ validate-headers
- ✅ validate-jwt
- ✅ validate-odata-request
- ✅ validate-parameters
- ✅ validate-status-code
- ✅ wait
- ✅ xml-to-json
- ✅ xsl-transform

## Policies not yet implemented

The following Azure API Management policies are not yet supported as first-class C# APIs. Use `InlinePolicy(...)` as
a workaround.

- validate-graphql-request
- sql-data-source (GraphQL resolver)
- cosmosdb-data-source (GraphQL resolver)
- http-data-source (GraphQL resolver)
- publish-event (GraphQL subscription)

## C# authoring examples

### Validation policies

```csharp
// Validate headers against API schema
c.ValidateHeaders(new ValidateHeadersConfig
{
    SpecifiedHeaderAction = "prevent",
    UnspecifiedHeaderAction = "ignore",
    ErrorsVariableName = "header-errors"
});

// Validate query/path parameters
c.ValidateParameters(new ValidateParametersConfig
{
    SpecifiedParameterAction = "prevent",
    UnspecifiedParameterAction = "prevent",
    ErrorsVariableName = "param-errors"
});

// Validate response status codes
c.ValidateStatusCode(new ValidateStatusCodeConfig
{
    UnspecifiedStatusCodeAction = "prevent",
    ErrorsVariableName = "statuscode-errors"
});

// Validate OData requests
c.ValidateOdataRequest(new ValidateOdataRequestConfig
{
    ErrorVariableName = "odata-errors",
    DefaultOdataVersion = "4.0",
    MaxSize = 10000
});

// Validate request/response content
c.ValidateContent(new ValidateContentConfig
{
    UnspecifiedContentTypeAction = "prevent",
    MaxSize = 102400,
    SizeExceededAction = "prevent",
    ErrorsVariableName = "content-errors"
});

// Validate client certificate
c.ValidateClientCertificate(new ValidateClientCertificateConfig
{
    ValidateRevocation = true,
    ValidateTrust = true,
    ValidateNotBefore = true,
    ValidateNotAfter = true
});

// Validate Azure AD token
c.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
{
    HeaderName = "Authorization",
    FailedValidationHttpCode = 401,
    FailedValidationErrorMessage = "Unauthorized",
    Audiences = new[] { "https://api.example.com" },
    RequiredClaims = new ClaimConfig[]
    {
        new() { Name = "roles", Match = "any", Values = new[] { "admin", "reader" } }
    }
});
```

### XSL transform

```csharp
c.XslTransform(new XslTransformConfig
{
    Xsl = "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">" +
          "<xsl:output method=\"xml\" indent=\"yes\"/>" +
          "<xsl:template match=\"/\"><root><xsl:value-of select=\".\"/></root></xsl:template>" +
          "</xsl:stylesheet>"
});
```

### Cross-domain access

```csharp
c.CrossDomain("<cross-domain-policy><allow-access-from domain=\"*\" /></cross-domain-policy>");
```

### HTTP proxy

```csharp
c.Proxy(new ProxyConfig
{
    Url = "http://proxy.example.com:8080",
    Username = "admin",
    Password = "secret"
});
```

### Integration policies

```csharp
// Send Azure Service Bus message
c.SendServiceBusMessage(new SendServiceBusMessageConfig
{
    QueueName = "my-queue",
    Namespace = "my-namespace.servicebus.windows.net"
});

// Invoke Dapr binding
c.InvokeDarpBinding(new InvokeDarpBindingConfig
{
    Name = "my-binding",
    Operation = "create",
    IgnoreError = true
});

// Publish to Dapr topic
c.PublishToDarp(new PublishToDarpConfig
{
    Topic = "my-topic",
    PubSubName = "pubsub"
});
```

### AI gateway policies

```csharp
// Content safety check for LLM requests
c.LlmContentSafety(new LlmContentSafetyConfig
{
    BackendId = "content-safety-backend"
});

// Azure OpenAI token limit
c.AzureOpenAiTokenLimit(new TokenLimitConfig
{
    CounterKey = "my-key",
    TokensPerMinute = 10000,
    EstimatePromptTokens = true,
    RemainingTokensHeaderName = "x-remaining-tokens"
});

// LLM token metrics
c.LlmEmitTokenMetric(new EmitTokenMetricConfig
{
    Namespace = "my-namespace",
    Dimensions = new MetricDimensionConfig[]
    {
        new() { Name = "API ID", Value = "@(context.Api.Id)" }
    }
});

// Semantic cache lookup
c.AzureOpenAiSemanticCacheLookup(new SemanticCacheLookupConfig
{
    EmbeddingsBackendId = "embeddings-backend",
    ScoreThreshold = 0.8,
    EmbeddingsModelName = "text-embedding-ada-002"
});
```

## How to work around missing policies

InlinePolicy is a workaround until all the policies are implemented or new policies are not added yet to toolkit.
It allows you to include policy not implemented yet to the document.

```csharp
c.InlinePolicy("<set-backend-service base-url=\"https://internal.contoso.example\" />");
```

## Contributing

If you'd like a specific policy implemented natively in the toolkit, please open an issue or a pull request in this
repository. See [CONTRIBUTING.md](../CONTRIBUTING.md) and [Add new policy guide](AddPolicyGuide.md) for guidance.
