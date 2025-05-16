using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public class CertificateStore
{
    internal readonly Dictionary<string, X509Certificate2> ById = new();
    internal readonly Dictionary<string, X509Certificate2> ByThumbprint = new();

    public CertificateStore WithCertificateById(string id, X509Certificate2 certificate)
    {
        ById.Add(id, certificate);
        return this;
    }

    public CertificateStore WithCertificateByThumbprint(string thumbprint, X509Certificate2 certificate)
    {
        ByThumbprint.Add(thumbprint, certificate);
        return this;
    }
}