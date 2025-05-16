// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Sockets;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

public static class IPAddressExtensions
{
    public static int CompareTo(this IPAddress? left, IPAddress? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return 1;
        }

        if (right is null)
        {
            return -1;
        }

        var leftBytesSpan = new ReadOnlySpan<byte>(left.GetAddressBytes());
        var rightBytesSpan = new ReadOnlySpan<byte>(right.GetAddressBytes());
        if (left.AddressFamily == right.AddressFamily)
        {
            return leftBytesSpan.SequenceCompareTo(rightBytesSpan);
        }

        return left.AddressFamily == AddressFamily.InterNetwork
            ? -Compare(rightBytesSpan, leftBytesSpan)
            : Compare(leftBytesSpan, rightBytesSpan);
    }

    // IPv6 need to have the following prefix to be treated as IPv4 (first 12 bytes)
    private static byte[] ipv4PrefixInIpv6 = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 255, 255];

    private static int Compare(ReadOnlySpan<byte> ipv6BytesSpan, ReadOnlySpan<byte> ipv4BytesSpan)
    {
        var prefixCompare = ipv6BytesSpan[..ipv4PrefixInIpv6.Length].SequenceCompareTo(ipv4PrefixInIpv6);
        if (prefixCompare != 0)
        {
            return prefixCompare;
        }

        var ipv4InIpv6Span = ipv6BytesSpan[ipv4PrefixInIpv6.Length..];
        return ipv4InIpv6Span.SequenceCompareTo(ipv4BytesSpan);
    }
}