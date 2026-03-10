// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public readonly struct ConfigValue
{
    private readonly string? _value;

    public ConfigValue(string? value) => _value = value;

    public static implicit operator string?(ConfigValue cv) => cv._value;
    public static implicit operator int(ConfigValue cv) =>
        int.TryParse(cv._value, out var r) ? r : throw new FormatException($"Cannot convert '{cv._value}' to int.");
    public static implicit operator bool(ConfigValue cv) =>
        bool.TryParse(cv._value, out var r) ? r : throw new FormatException($"Cannot convert '{cv._value}' to bool.");
    public static implicit operator long(ConfigValue cv) =>
        long.TryParse(cv._value, out var r) ? r : throw new FormatException($"Cannot convert '{cv._value}' to long.");
    public static implicit operator double(ConfigValue cv) =>
        double.TryParse(cv._value, out var r) ? r : throw new FormatException($"Cannot convert '{cv._value}' to double.");
    public static implicit operator Guid(ConfigValue cv) =>
        Guid.TryParse(cv._value, out var r) ? r : throw new FormatException($"Cannot convert '{cv._value}' to Guid.");
    public static implicit operator JToken(ConfigValue cv) =>
        cv._value is not null ? JValue.CreateString(cv._value) : JValue.CreateNull();

    public override string? ToString() => _value;
}
