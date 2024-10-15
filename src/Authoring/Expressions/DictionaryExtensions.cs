﻿namespace Mielek.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public static class DictionaryExtensions
{
    public static string? GetValueOrDefault(
        this IReadOnlyDictionary<string, string[]> dictionary,
        string headerName)
        => dictionary.TryGetValue(headerName, out var value) ? string.Join(',', value) : null;
    
    public static string GetValueOrDefault(
        this IReadOnlyDictionary<string, string[]> dictionary,
        string headerName,
        string defaultValue)
        => dictionary.TryGetValue(headerName, out var value) ? string.Join(',', value) : defaultValue;

    public static T? GetValueOrDefault<T>(
        this IReadOnlyDictionary<string, object> dictionary,
        string variableName)
        => dictionary.TryGetValue(variableName, out var value) && value is T casted ? casted : default;
    
    public static T GetValueOrDefault<T>(
        this IReadOnlyDictionary<string, object> dictionary,
        string variableName,
        T defaultValue)
        => dictionary.TryGetValue(variableName, out var value) && value is T casted ? casted : defaultValue;

    public static string ToQueryString(this IDictionary<string, IList<string>> dictionary)
        => "";

    public static string ToFormUrlEncodedContent(this IDictionary<string, IList<string>> dictionary)
        => "";
}