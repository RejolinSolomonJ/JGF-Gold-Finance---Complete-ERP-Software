using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Application.Security;

public record AuthResult(bool Succeeded, string? ErrorMessage = null, User? User = null);

public record CompanyProfileDto(
    string CompanyName,
    string RegistrationNumber,
    string Address,
    string Phone,
    string? LogoPath,
    string? GstNumber,
    string FinancialYear = "2024-25"
);

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password, string? ipAddress = null, CancellationToken ct = default);
    Task<bool> IsFirstRunAsync(CancellationToken ct = default);
    Task<User> CompleteFirstRunWizardAsync(string ownerUsername, string ownerPassword, string ownerFullName, CompanyProfileDto company, CancellationToken ct = default);
}

