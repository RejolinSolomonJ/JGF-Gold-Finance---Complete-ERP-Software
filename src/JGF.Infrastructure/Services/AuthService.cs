using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JGF.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly JgfDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        JgfDbContext context,
        IPasswordHasher passwordHasher,
        ILogger<AuthService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<bool> IsFirstRunAsync(CancellationToken ct = default)
    {
        return !await _context.Users.AnyAsync(ct);
    }

    public async Task<AuthResult> LoginAsync(string username, string password, string? ipAddress = null, CancellationToken ct = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user == null)
        {
            await LogLoginAttemptAsync(null, username, AuditAction.FailedLogin, ipAddress, "User does not exist", ct);
            return new AuthResult(false, "Invalid username or password.");
        }

        if (user.LockedUntilUtc.HasValue && user.LockedUntilUtc.Value > DateTime.UtcNow)
        {
            var remaining = user.LockedUntilUtc.Value.Subtract(DateTime.UtcNow).TotalMinutes;
            return new AuthResult(false, $"Account is locked due to multiple failed attempts. Try again in {Math.Ceiling(remaining)} minutes.");
        }

        if (!user.IsActive)
        {
            return new AuthResult(false, "User account is disabled. Please contact the Owner.");
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(password, user.PasswordHash);
        if (!isPasswordValid)
        {
            user.FailedLoginAttempts += 1;
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockedUntilUtc = DateTime.UtcNow.AddMinutes(15);
                _logger.LogWarning("User {Username} locked out after 5 failed attempts.", username);
            }
            await _context.SaveChangesAsync(ct);
            await LogLoginAttemptAsync(user.Id, username, AuditAction.FailedLogin, ipAddress, "Invalid password", ct);
            return new AuthResult(false, "Invalid username or password.");
        }

        // Login succeeded
        user.FailedLoginAttempts = 0;
        user.LockedUntilUtc = null;
        user.LastLoginAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        await LogLoginAttemptAsync(user.Id, username, AuditAction.Login, ipAddress, "Successful login", ct);
        return new AuthResult(true, null, user);
    }

    public async Task<User> CompleteFirstRunWizardAsync(
        string ownerUsername,
        string ownerPassword,
        string ownerFullName,
        CompanyProfileDto company,
        CancellationToken ct = default)
    {
        if (await _context.Users.AnyAsync(ct))
        {
            throw new InvalidOperationException("First-run setup has already been completed.");
        }

        var owner = new User
        {
            Username = ownerUsername,
            PasswordHash = _passwordHasher.HashPassword(ownerPassword),
            FullName = ownerFullName,
            Role = UserRole.Owner,
            IsActive = true
        };

        await _context.Users.AddAsync(owner, ct);

        // Store company details in Settings
        var settings = new List<Setting>
        {
            new() { Key = "Company.Name", Value = company.CompanyName, DisplayName = "Company Name", Category = "Company" },
            new() { Key = "Company.RegNo", Value = company.RegistrationNumber, DisplayName = "Registration Number", Category = "Company" },
            new() { Key = "Company.Address", Value = company.Address, DisplayName = "Address", Category = "Company" },
            new() { Key = "Company.Phone", Value = company.Phone, DisplayName = "Phone", Category = "Company" },
            new() { Key = "Company.GstNo", Value = company.GstNumber ?? "", DisplayName = "GST Number", Category = "Company" },
            new() { Key = "Company.LogoPath", Value = company.LogoPath ?? "", DisplayName = "Logo File Path", Category = "Company" },
            new() { Key = "Company.FinancialYear", Value = company.FinancialYear, DisplayName = "Active Financial Year", Category = "Company" }
        };

        await _context.Settings.AddRangeAsync(settings, ct);
        await _context.SaveChangesAsync(ct);

        return owner;
    }

    private async Task LogLoginAttemptAsync(Guid? userId, string username, AuditAction action, string? ipAddress, string reason, CancellationToken ct)
    {
        var audit = new AuditLog
        {
            TableName = "Users",
            RecordId = userId,
            Action = action,
            Timestamp = DateTime.UtcNow,
            UserId = userId,
            UserName = username,
            NewValues = $"{{\"Event\": \"{action}\", \"Reason\": \"{reason}\"}}",
            IpAddress = ipAddress
        };
        await _context.AuditLogs.AddAsync(audit, ct);
        await _context.SaveChangesAsync(ct);
    }
}
