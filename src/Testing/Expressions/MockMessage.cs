namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockMessage
{
    public MockBody Body { get; set; } = new MockBody();
    public Dictionary<string, string[]> Headers { get; set; } = new Dictionary<string, string[]>();
}