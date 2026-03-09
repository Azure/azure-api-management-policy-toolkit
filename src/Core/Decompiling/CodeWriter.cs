// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling;

public class CodeWriter
{
    private readonly StringBuilder _sb = new();
    private int _indentLevel;

    public void IncreaseIndent() => _indentLevel++;
    public void DecreaseIndent() => _indentLevel--;

    public void AppendLine(string text)
    {
        _sb.Append(new string(' ', _indentLevel * 4));
        _sb.AppendLine(text);
    }

    public void AppendLine() => _sb.AppendLine();

    public void Append(string text)
    {
        _sb.Append(new string(' ', _indentLevel * 4));
        _sb.Append(text);
    }

    public void AppendRaw(string text) => _sb.Append(text);

    public override string ToString() => _sb.ToString();
}
