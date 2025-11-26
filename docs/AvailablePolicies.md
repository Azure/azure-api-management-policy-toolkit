# Available Policies

This document lists policy elements that the toolkit's authoring and compiler support by mapping C# APIs to Azure API
Management policy elements. The list below was generated from the public authoring interfaces (
inbound/outbound/backend/on-error/fragment contexts) and reflects which policies the toolkit can compile into XML.

If a policy you need is missing you can either:

- Use `InlinePolicy(...)` to insert raw XML into the compiled document, or
- Open an issue / contribute a patch (see [CONTRIBUTING.md](../CONTRIBUTING.md)).

Notes:

- The C# compiler maps C# constructs to policy constructs. For example, if/else in C# is compiled to the `choose`
  policy (with `when` / `otherwise`).
- `InlinePolicy(string)` allows inserting arbitrary XML when a policy isn't implemented as a first-class API.

## Implemented policies

- authentication-basic
- authentication-certificate
- authentication-managed-identity
- azure-openai-emit-token-metric
- azure-openai-semantic-cache-lookup
- azure-openai-semantic-cache-store
- azure-openai-token-limit
- base
- cache-lookup
- cache-lookup-value
- cache-remove-value
- cache-store
- cache-store-value
- check-header
- choose (implemented via C# if/else -> choose/when/otherwise)
- cors
- cross-domain
- emit-metric
- find-and-replace
- forward-request
- get-authorization-context
- include-fragment
- inline-policy (method to insert raw XML)
- invoke-dapr-binding (publish/send to Dapr bindings)
- ip-filter
- json-to-xml
- jsonp
- limit-concurrency
- llm-content-safety
- llm-emit-token-metric
- llm-semantic-cache-lookup
- llm-semantic-cache-store
- llm-token-limit
- log-to-eventhub
- mock-response
- proxy
- publish-to-dapr
- quota
- quota-by-key
- rate-limit
- rate-limit-by-key
- redirect-content-urls
- remove-header (via SetHeader/RemoveHeader APIs)
- remove-query-parameter (via SetQueryParameter/Remove APIs)
- return-response
- retry
- rewrite-uri
- send-one-way-request
- send-request
- set-backend-service
- set-body
- set-header
- set-header-if-not-exist
- set-method
- set-query-parameter
- set-query-parameter-if-not-exist
- set-status
- set-variable
- trace
- validate-azure-ad-token
- validate-client-certificate
- validate-content
- validate-headers
- validate-jwt
- validate-odata-request
- validate-parameters
- validate-status-code
- wait
- xml-to-json
- xsl-transform

## How to work around missing policies

InlinePolicy is a workaround until all the policies are implemented or new policies are not added yet to toolkit.
It allows you to include policy not implemented yet to the document.

```csharp
c.InlinePolicy("<set-backend-service base-url=\"https://internal.contoso.example\" />");
```

## Contributing

If you'd like a specific policy implemented natively in the toolkit, please open an issue or a pull request in this
repository. See [CONTRIBUTING.md](../CONTRIBUTING.md) and [Add new policy guide](AddPolicyGuide.md) for guidance.
