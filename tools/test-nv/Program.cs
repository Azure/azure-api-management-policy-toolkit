using Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling;
var files = new[] { "send-request-cache-value.xml", "cors-auth-cert.xml", "named-value-scenarios.xml" };
var d = new PolicyDecompiler();
foreach (var f in files) {
    var xml = File.ReadAllText($@"Q:\frank\codegen-1772830392459-t5ncbt\test\Test.Decompiling\TestData\{f}");
    var code = d.DecompileDocument(xml, "T", "NS");
    var hasTokens = System.Text.RegularExpressions.Regex.IsMatch(code, @"\{\{[A-Za-z]");
    Console.WriteLine($"{f}: {(hasTokens ? "HAS TOKENS" : "CLEAN")}");
}
