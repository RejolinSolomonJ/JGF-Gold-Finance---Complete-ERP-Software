using JGF.Domain.Entities;
using JGF.Domain.Enums;

namespace JGF.Application.Security;

/// <summary>
/// Service to determine permissions based on UserRole and context.
/// </summary>
public interface IPermissionService
{
    bool HasPermission(UserRole role, Permission permission);
    void EnsurePermission(UserRole role, Permission permission);
}

public class PermissionService : IPermissionService
{
    private static readonly Dictionary<UserRole, HashSet<Permission>> RolePermissions = new()
    {
        [UserRole.Owner] = Enum.GetValues<Permission>().Where(p => p != Permission.None).ToHashSet(),

        [UserRole.Manager] = new HashSet<Permission>
        {
            Permission.ViewCustomer, Permission.CreateCustomer, Permission.EditCustomer,
            Permission.ViewLoan, Permission.CreateLoan, Permission.EditLoan, Permission.ApproveLoan, Permission.CloseLoan,
            Permission.ViewReceipt, Permission.CreateReceipt, Permission.CancelReceipt,
            Permission.ManageJewelTypes, Permission.ManageGoldRates, Permission.ManageBanks,
            Permission.ViewAccounts, Permission.ManageVouchers, Permission.CloseDay,
            Permission.ManageBankRepledge, Permission.ManageAuction,
            Permission.ViewReports, Permission.ManageBackups
        },

        [UserRole.Cashier] = new HashSet<Permission>
        {
            Permission.ViewCustomer, Permission.CreateCustomer,
            Permission.ViewLoan, Permission.CreateLoan,
            Permission.ViewReceipt, Permission.CreateReceipt,
            Permission.ViewReports
        }
    };

    public bool HasPermission(UserRole role, Permission permission)
    {
        if (RolePermissions.TryGetValue(role, out var perms))
        {
            return perms.Contains(permission);
        }
        return false;
    }

    public void EnsurePermission(UserRole role, Permission permission)
    {
        if (!HasPermission(role, permission))
        {
            throw new UnauthorizedAccessException($"User role '{role}' does not have the required permission: '{permission}'.");
        }
    }
}

