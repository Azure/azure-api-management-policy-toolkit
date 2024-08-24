namespace Mielek.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
public interface ILastError
{
    string Source { get; }
    string Reason { get; }
    string Message { get; }
    string Scope { get; }
    string Section { get; }
    string Path { get; }
    string PolicyId { get; }
}