namespace Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public record CacheValue(object Value)
{
    public uint? Duration { get; init; }
}