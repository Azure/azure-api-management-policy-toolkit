# Output format (`rawxml` vs `xml`)

Azure API Management accepts policy documents in two content formats: `rawxml` and `xml`. The compiler can emit
either one through the `--policy-format` option (alias `--pf`). Choose the format that matches how you deploy the
generated documents.

```shell
# default: rawxml
dotnet azure-apim-policy-compiler --s .\src --o .\target --format true

# standards-compliant xml
dotnet azure-apim-policy-compiler --s .\src --o .\target --format true --policy-format xml
```

| `--policy-format` | Expressions in the output | Deploy with content format |
| --- | --- | --- |
| `rawxml` (default) | Written verbatim, reserved XML characters left unescaped (for example `As<JObject>()`, `StartsWith("/v1/")`) | `rawxml` |
| `xml` | Reserved characters XML-encoded (for example `As&lt;JObject&gt;()`, `StartsWith(&quot;/v1/&quot;)`) | `xml` |

The generated document is only valid for the matching API Management content format. For example, a
`Microsoft.ApiManagement/service/apis/policies` or `.../policyFragments` Bicep resource has a `format` property that
must be set to the same value (`rawxml` or `xml`) you compiled with.

## Which format should I use?

- **`rawxml` (default)** keeps expressions verbatim, so the generated files stay readable — including multi-line
  expressions. This is the recommended format for most solutions. Deploy the files with the `rawxml` content
  format.
- **`xml`** produces standards-compliant XML. Use it when your deployment path requires the `xml` content format.
  Because the output is byte-for-byte closer to what the service stores, it also avoids false `What-If` differences
  during deployment when the `xml` content format is used.

## Drawback of the `xml` format: multi-line attribute expressions

In the `xml` format, a multi-line expression placed in an **attribute** (for example a
`set-variable value="@{ ... }"`) is collapsed onto a single physical line, with its line breaks encoded as `&#xA;`
character references:

```xml
<set-variable name="result" value="@{&#xD;&#xA;    var x = 1;&#xD;&#xA;    return x;&#xD;&#xA;}" />
```

This is required by the XML specification: literal newlines in attribute values are normalized to spaces by any
standards-compliant XML parser, so they must be encoded as character references to be preserved.

- **The code stays functionally correct.** API Management decodes the references back into real line breaks, so the
  expression behaves exactly as written.
- **Only attributes are affected.** Expressions in element content (for example a `<value>` or `<message>` body)
  are not subject to attribute-value normalization and remain formatted across multiple lines.

If readability of multi-line attribute expressions matters more than emitting the `xml` content format, keep the
default `rawxml` format and deploy it with API Management's `rawxml` content type.
