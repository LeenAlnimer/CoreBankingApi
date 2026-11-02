
using BankTask.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace BankTask.Infrastructure.Security;

public class GoogleTokenProtector : IGoogleTokenProtector
{
    private readonly IDataProtector _dataProtector;

    public GoogleTokenProtector(
        IDataProtectionProvider dataProtectionProvider)
    {
        _dataProtector = dataProtectionProvider.CreateProtector(
            "BankTask.GoogleOAuthTokens.v1");
    }

    public string Protect(string token)
    {
        return _dataProtector.Protect(token);
    }

    public string Unprotect(string protectedToken)
    {
        return _dataProtector.Unprotect(protectedToken);
    }
}
