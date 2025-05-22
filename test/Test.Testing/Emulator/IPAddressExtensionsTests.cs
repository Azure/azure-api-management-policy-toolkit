// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Test.Emulator.Emulator;

[TestClass]
public class IPAddressExtensionsTests
{
    [TestMethod]
    [DataRow(null, null, 0)]
    [DataRow(null, "1.1.1.1", 1)]
    [DataRow("1.1.1.1", null, -1)]
    [DataRow("1.1.1.1", "1.1.1.1", 0)]
    [DataRow("1.1.1.1", "1.1.1.2", -1)]
    [DataRow("1.1.1.2", "1.1.1.1", 1)]
    [DataRow("1.1.1.1", "1.1.2.1", -1)]
    [DataRow("1.1.2.1", "1.1.1.1", 1)]
    [DataRow("1.1.1.1", "1.2.1.1", -1)]
    [DataRow("1.2.1.1", "1.1.1.1", 1)]
    [DataRow("1.1.1.1", "2.1.1.1", -1)]
    [DataRow("2.1.1.1", "1.1.1.1", 1)]
    [DataRow(null, "::1", 1)]
    [DataRow("::1", null, -1)]
    [DataRow("::1", "::1", 0)]
    [DataRow("::1", "::2", -1)]
    [DataRow("::2", "::1", 1)]
    [DataRow("::0201", "::0101", 1)]
    [DataRow("::0101", "::0201", -1)]
    [DataRow("::0102:0101", "::0101:0101", 1)]
    [DataRow("::0101:0101", "::0102:0101", -1)]
    [DataRow("::ffff:0101:0101", "1.1.1.1", 0)]
    [DataRow("1.1.1.1", "::ffff:0101:0101", 0)]
    [DataRow("1.1.1.2", "::ffff:0101:0101", 1)]
    [DataRow("::ffff:0101:0101", "1.1.1.2", -1)]
    [DataRow("1.1.1.1", "::ffff:0101:0102", -1)]
    [DataRow("::ffff:0101:0102", "1.1.1.1", 1)]
    [DataRow("192.168.0.1", "10.0.0.1", 192 - 10)]
    [DataRow("10.0.0.1", "192.168.0.1", 10 - 192)]
    [DataRow("::fffe:0101:0101", "1.1.1.1", -1)] // 1.1.1.1 == ::ffff:0101:0101
    [DataRow("1.1.1.1", "::fffe:0101:0101", 1)] // 1.1.1.1 == ::ffff:0101:0101
    public void IPAddress_CompareTo(string? left, string? right, int value)
    {
        IPAddress? lAddress = IPAddress.TryParse(left, out var l) ? l : null;
        IPAddress? rAddress = IPAddress.TryParse(right, out var r) ? r : null;

        lAddress.CompareTo(rAddress).Should().Be(value);
    }
}