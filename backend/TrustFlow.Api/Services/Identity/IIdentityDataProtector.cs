namespace TrustFlow.Api.Services.Identity;

public interface IIdentityDataProtector
{
    byte[] Protect(byte[] value);
    byte[] Protect(string value);
    byte[] Unprotect(byte[] value);
    string UnprotectString(byte[] value);
}
