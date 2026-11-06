using System.Text.Json;
using JGF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace JGF.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that captures entity changes (Added, Modified, Deleted)
/// and writes immutable <see cref="AuditLog"/> entries with old/new values as JSON.
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Captures audit entries before SaveChanges to ensure old values are recorded.
    /// </summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var auditEntries = CollectAuditEntries(eventData.Context);
        eventData.Context.ChangeTracker.AutoDetectChangesEnabled = false;

        foreach (var entry in auditEntries)
        {
            eventData.Context.Set<AuditLog>().Add(entry);
        }

        eventData.Context.ChangeTracker.AutoDetectChangesEnabled = true;

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Synchronous override for SaveChanges.
    /// </summary>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is null)
            return base.SavingChanges(eventData, result);

        var auditEntries = CollectAuditEntries(eventData.Context);
        eventData.Context.ChangeTracker.AutoDetectChangesEnabled = false;

        foreach (var entry in auditEntries)
        {
            eventData.Context.Set<AuditLog>().Add(entry);
        }

        eventData.Context.ChangeTracker.AutoDetectChangesEnabled = true;

        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Iterates tracked entities and builds AuditLog entries for Added, Modified, and Deleted states.
    /// Skips the AuditLog entity itself to avoid infinite recursion.
    /// </summary>
    private static List<AuditLog> CollectAuditEntries(DbContext context)
    {
        var auditEntries = new List<AuditLog>();
        var utcNow = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Skip AuditLog itself to prevent recursion
            if (entry.Entity is AuditLog)
                continue;

            // Only track meaningful states
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var tableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name;
            var primaryKey = GetPrimaryKeyValue(entry);
            var action = entry.State.ToString();

            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();
            var affectedColumns = new List<string>();

            foreach (var property in entry.Properties)
            {
                var propertyName = property.Metadata.Name;

                switch (entry.State)
                {
                    case EntityState.Added:
                        newValues[propertyName] = property.CurrentValue;
                        affectedColumns.Add(propertyName);
                        break;

                    case EntityState.Deleted:
                        oldValues[propertyName] = property.OriginalValue;
                        affectedColumns.Add(propertyName);
                        break;

                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            oldValues[propertyName] = property.OriginalValue;
                            newValues[propertyName] = property.CurrentValue;
                            affectedColumns.Add(propertyName);
                        }
                        break;
                }
            }

            // Extract UserId from the entity if it has CreatedByUserId/UpdatedByUserId
            Guid? userId = null;
            string? userName = null;
            if (entry.Entity is Domain.Common.AuditableEntity auditable)
            {
                userId = entry.State == EntityState.Added
                    ? auditable.CreatedByUserId
                    : auditable.UpdatedByUserId;
            }

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                TableName = tableName,
                RecordId = primaryKey,
                Action = action,
                Timestamp = utcNow,
                UserId = userId,
                UserName = userName,
                OldValues = oldValues.Count > 0 ? JsonSerializer.Serialize(oldValues, JsonOptions) : null,
                NewValues = newValues.Count > 0 ? JsonSerializer.Serialize(newValues, JsonOptions) : null,
                AffectedColumns = affectedColumns.Count > 0 ? string.Join(",", affectedColumns) : null
            };

            auditEntries.Add(auditLog);
        }

        return auditEntries;
    }

    /// <summary>
    /// Extracts the primary key value from an entity entry as a string.
    /// </summary>
    private static string GetPrimaryKeyValue(EntityEntry entry)
    {
        var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties;
        if (keyProperties is null || keyProperties.Count == 0)
            return "unknown";

        var keyValues = keyProperties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "null");

        return string.Join("_", keyValues);
    }
}
