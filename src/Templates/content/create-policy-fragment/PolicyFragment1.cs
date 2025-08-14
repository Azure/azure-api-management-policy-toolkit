namespace Company.PolicyProject1;

[Document(Type = DocumentType.Fragment)]
public class PolicyFragment1 : IFragment
{
    public void Fragment(IFragmentContext context)
    {
        // Add reusable policy logic here
        // This fragment can be included in any policy section (inbound, outbound, backend, on-error)

        // Example: Set a common header
        // context.SetHeader("X-Custom-Header", "fragment-value");

        // Example: Set a variable
        // context.SetVariable("fragment-processed", "true");
    }
}
