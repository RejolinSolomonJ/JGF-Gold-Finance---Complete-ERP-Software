using FluentAssertions;
using JGF.Application.Security;
using JGF.Domain.Enums;
using Xunit;

namespace JGF.Tests.Security;

public class SecurityAndAuthTests
{
    private readonly BCryptPasswordHasher _hasher = new();
    private readonly PermissionService _permissions = new();

    [Fact]
    public void PasswordHasher_ShouldHashAndVerifyCorrectly()
    {
        var password = "SecurePassword@123";
        var hash = _hasher.HashPassword(password);

        hash.Should().NotBeNullOrWhiteSpace();
        _hasher.VerifyPassword(password, hash).Should().BeTrue();
        _hasher.VerifyPassword("WrongPassword", hash).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.Owner, Permission.ViewAuditLogs, true)]
    [InlineData(UserRole.Manager, Permission.ViewAuditLogs, false)]
    [InlineData(UserRole.Cashier, Permission.ViewAuditLogs, false)]
    [InlineData(UserRole.Manager, Permission.ManageJewelTypes, true)]
    [InlineData(UserRole.Cashier, Permission.ManageJewelTypes, false)]
    [InlineData(UserRole.Cashier, Permission.CreateReceipt, true)]
    [InlineData(UserRole.Cashier, Permission.CancelReceipt, false)]
    public void PermissionMatrix_ShouldEnforceRoleBoundaries(UserRole role, Permission permission, bool expected)
    {
        var hasAccess = _permissions.HasPermission(role, permission);
        hasAccess.Should().Be(expected);
    }
}

