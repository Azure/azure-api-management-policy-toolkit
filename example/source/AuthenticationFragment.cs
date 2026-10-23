using System.ComponentModel;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Example;

[Document("auth-fragment", Type = DocumentType.Fragment)]
public class AuthenticationFragment : IFragment
{
    [FragmentVariable("xauth", DefaultValue = "false")]
    public string XAuthFragmentValue { get; set; }

    public void Fragment(IFragmentContext context)
    {
        context.SetHeader("X-Auth-Fragment", XAuthFragmentValue);
        context.AuthenticationBasic("{{username}}", "{{password}}");
    }
}
