namespace Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
public interface ISubscription
{
    DateTime CreatedDate { get; }
    DateTime? EndDate { get; }
    string Id { get; }
    string Key { get; }
    string Name { get; }
    string PrimaryKey { get; }
    string SecondaryKey { get; }
    DateTime? StartDate { get; }
}