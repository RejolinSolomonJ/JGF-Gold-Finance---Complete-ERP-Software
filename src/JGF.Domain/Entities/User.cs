using System.ComponentModel.DataAnnotations;
using JGF.Domain.Common;
using JGF.Domain.Enums;

namespace JGF.Domain.Entities;

/// <summary>
/// Application user account with role-based access.
/// </summary>
public class User : AuditableEntity, ISoftDeletable
{
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(15)]
    public string? MobileNumber { get; set; }

    [Required]
    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public int FailedLoginAttempts { get; set; } = 0;

    public DateTime? LockedUntilUtc { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }

    [MaxLength(255)]
    public string? RefreshToken { get; set; }

    public DateTime? RefreshTokenExpiryUtc { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

