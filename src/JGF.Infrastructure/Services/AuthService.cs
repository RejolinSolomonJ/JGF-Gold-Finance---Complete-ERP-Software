using JGF.Application.Security;
using JGF.Application.Services;
using JGF.Domain.Entities;
using JGF.Domain.Enums;
using JGF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JGF.Infrastructure.Services;

public class AuthService : DbServiceBase, IAuthService
{
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AuthService> _logger;

    [ActivatorUtilitiesConstructor]
    public AuthService(
        IDbContextFactory<JgfDbContext> contextFactory,
        IPasswordHasher passwordHasher,
        ILogger<AuthService> logger)
        : base(contextFactory)
    {
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    internal AuthService(
        JgfDbContext context,
        IPasswordHasher passwordHasher,
        ILogger<AuthService> logger)
        : base(context)
    {
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<bool> IsFirstRunAsync(CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        return !await scope.Context.Users.AsNoTracking().AnyAsync(ct);
    }

    public async Task<AuthResult> LoginAsync(string username, string password, string? ipAddress = null, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var user = await context.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user == null)
        {
            await LogLoginAttemptAsync(context, null, username, AuditAction.FailedLogin, ipAddress, "User does not exist", ct);
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

        var isValid = _passwordHasher.VerifyPassword(password, user.PasswordHash);
        if (!isValid)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockedUntilUtc = DateTime.UtcNow.AddMinutes(15);
                _logger.LogWarning("User {Username} locked out due to excessive failed attempts.", username);
            }
            await context.SaveChangesAsync(ct);
            await LogLoginAttemptAsync(context, user.Id, username, AuditAction.FailedLogin, ipAddress, $"Failed attempt {user.FailedLoginAttempts}", ct);
            return new AuthResult(false, "Invalid username or password.");
        }

        // Successful login
        user.FailedLoginAttempts = 0;
        user.LockedUntilUtc = null;
        user.LastLoginAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        await LogLoginAttemptAsync(context, user.Id, username, AuditAction.Login, ipAddress, "Successful login", ct);
        return new AuthResult(true, null, user);
    }

    public async Task<User> CompleteFirstRunWizardAsync(
        string ownerUsername,
        string ownerPassword,
        string ownerFullName,
        CompanyProfileDto company,
        CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        if (await context.Users.AnyAsync(ct))
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

        await context.Users.AddAsync(owner, ct);

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

        await context.Settings.AddRangeAsync(settings, ct);
        await context.SaveChangesAsync(ct);

        return owner;
    }

    public async Task<User> CreateUserAsync(string username, string password, string fullName, UserRole role, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var exists = await context.Users.AnyAsync(u => u.Username == username, ct);
        if (exists)
        {
            throw new InvalidOperationException($"Username '{username}' already exists.");
        }

        var user = new User
        {
            Username = username,
            PasswordHash = _passwordHasher.HashPassword(password),
            FullName = fullName,
            Role = role,
            IsActive = true
        };

        await context.Users.AddAsync(user, ct);
        await context.SaveChangesAsync(ct);
        return user;
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string oldPassword, string newPassword, CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        var context = scope.Context;

        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return false;

        if (!_passwordHasher.VerifyPassword(oldPassword, user.PasswordHash))
        {
            return false;
        }

        user.PasswordHash = _passwordHasher.HashPassword(newPassword);
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<User>> GetAllUsersAsync(CancellationToken ct = default)
    {
        await using var scope = await GetContextAsync(ct);
        return await scope.Context.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync(ct);
    }

    private async Task LogLoginAttemptAsync(JgfDbContext context, Guid? userId, string username, AuditAction action, string? ipAddress, string notes, CancellationToken ct)
    {
        try
        {
            var log = new AuditLog
            {
                UserId = userId,
                UserName = username,
                Action = action,
                TableName = "Users",
                IpAddress = ipAddress,
                NewValues = notes,
                Timestamp = DateTime.UtcNow
            };
            await context.AuditLogs.AddAsync(log, ct);
            await context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for user {Username}", username);
        }
    }
}
