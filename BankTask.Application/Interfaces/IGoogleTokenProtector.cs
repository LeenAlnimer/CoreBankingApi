
namespace BankTask.Application.Interfaces;

public interface IGoogleTokenProtector
{
    string Protect(string token);

    string Unprotect(string protectedToken);
}
