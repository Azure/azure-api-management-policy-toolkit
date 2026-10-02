// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockMessage
{
    private Dictionary<string, string[]> _headers = new(StringComparer.OrdinalIgnoreCase);

    public MockBody Body { get; set; } = new MockBody();

    /// <summary>
    /// Gets or sets the dictionary used to store message headers.
    /// </summary>
    /// <remarks>
    /// The default dictionary uses <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// Assigning a dictionary preserves its instance, comparer, keys, and values unchanged.
    /// Header name comparisons in an assigned dictionary follow its supplied comparer.
    /// </remarks>
    public Dictionary<string, string[]> Headers
    {
        get => _headers;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _headers = value;
        }
    }
}