using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Company.PolicyProject1;

[Document]
public class PolicyDocument1 : IDocument
{
#if (Sections == Inbound)
    public void Inbound(IInboundContext context)
    {
        // Throttle, authorize, validate, cache, or transform the requests
    }

#endif
#if (Sections == Backend)
    public void Backend(IBackendContext context)
    {
        // Control if and how the requests are forwarded to services
    }
    
#endif
#if (Sections == Outbound)
    public void Outbound(IOutboundContext context)
    {
        // Customize the responses
    }
    
#endif
#if (Sections == OnError)
    public void OnError(IOnErrorContext context)
    {
        // Handle exceptions and customize error responses
    }
#endif
}