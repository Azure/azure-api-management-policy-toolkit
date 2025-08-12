using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Example;

[Document("authentication-fragment", type: DocumentType.Fragment)]
public class AuthenticationFragment : IFragment
{
    public void Fragment(IFragmentContext context)
    {
        context.SetHeader("X-Auth-Fragment", "true");
        context.AuthenticationBasic("{{username}}", "{{password}}");
    }
}
