using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace TrustFlow.Api.Services.Identity;

public sealed class IdentityDataProtector(IDataProtectionProvider provider) : IIdentityDataProtector
{
    private readonly IDataProtector protector =
        provider.CreateProtector("TrustFlow.ClientIdentityVerification.v1");

    public byte[] Protect(byte[] value) => protector.Protect(value);
    public byte[] Protect(string value) => Protect(Encoding.UTF8.GetBytes(value));
    public byte[] Unprotect(byte[] value) => protector.Unprotect(value);
    public string UnprotectString(byte[] value) => Encoding.UTF8.GetString(Unprotect(value));
}
