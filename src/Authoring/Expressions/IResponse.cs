namespace Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
public interface IResponse
{
    IMessageBody Body { get; }
    IReadOnlyDictionary<string, string[]> Headers { get; }
    int StatusCode { get; }
    string StatusReason { get; }
}