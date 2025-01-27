using Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class CacheLookupTests
{
    private class SimpleCacheLookup : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.CacheLookup(new CacheLookupConfig { VaryByDeveloper = false, VaryByDeveloperGroups = false });
        }
    }

    private class SimpleCacheLookup1 : IDocument
    {
        public void Outbound(IOutboundContext context)
        {
            context.CacheStore(10, true);
        }
    }
}