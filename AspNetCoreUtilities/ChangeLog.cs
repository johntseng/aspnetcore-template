using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace AspNetCoreUtilities
{
    /// <summary>
    /// Holds changes that are written through Entity Framework.
    /// </summary>
    public class ChangeLog : IDbEntity
    {
        public long Id { get; set; }
        // Time that the change occurred.
        public DateTime Time { get; set; }
        // The entities that were changed.
        public virtual ICollection<ChangeLogEntity> ChangeLogEntities { get; set; }
        
        // Records the changes after saving the changes.
        public static async Task<int> SaveWithTracking<T>(T context, Func<Task<int>> saveChanges)
            where T : DbContext, IHasChangeLogs
        {
            var changeLog = new ChangeLog
            {
                Time = DateTime.UtcNow,
                ChangeLogEntities = context.ChangeTracker
                    .Entries()
                    .Where(e =>
                        e.State == EntityState.Added
                        || e.State == EntityState.Deleted
                        || e.State == EntityState.Modified)
                    .Select(entry =>
                    {
                        var changeLogEntity = new ChangeLogEntity
                        {
                            EntityType = entry.Metadata.ClrType.Name,
                            Action = entry.State.ToString(),
                            ChangeLogProperties = entry
                                .Properties
                                .Where(p => p.IsModified)
                                .Select(p => new ChangeLogProperty
                                {
                                    Name = p.Metadata.Name,
                                    OriginalValue = p.OriginalValue?.ToString(),
                                    CurrentValue = p.CurrentValue?.ToString()
                                })
                                .ToList(),
                            EntityKeyGetter = () => JsonConvert.SerializeObject(
                                entry.Metadata
                                    .FindPrimaryKey()
                                    .Properties
                                    .OrderBy(p => p.Name)
                                    .ToDictionary(p => p.Name, p => entry.CurrentValues[p.Name]))
                        };

                        if (entry.Entity is ISoftDelete
                            && entry.State == EntityState.Modified
                            && (bool)entry.OriginalValues["IsDeleted"] == false
                            && (bool)entry.CurrentValues["IsDeleted"] == true)
                        {
                            changeLogEntity.Action = EntityState.Deleted.ToString();
                        }

                        return changeLogEntity;
                    })
                    .ToList()
            };
            var changeCount = await saveChanges();
            if (changeLog.ChangeLogEntities.Any())
            {
                context.ChangeLogs.Add(changeLog);
                foreach (var entity in changeLog.ChangeLogEntities)
                {
                    entity.EntityKey = entity.EntityKeyGetter();
                }
                await saveChanges();
            }
            return changeCount;
        }

        // Records the changes after saving the changes.
        public static int SaveWithTracking<T>(T context, Func<int> saveChanges)
            where T : DbContext, IHasChangeLogs => 
            SaveWithTracking(context,() => Task.FromResult(saveChanges())).Result;
    }

    /// <summary>
    /// An entity that was changed.
    /// </summary>
    public class ChangeLogEntity : IDbEntity
    {
        public long Id { get; set; }
        // The ChangeLog that contains this ChangeLogEntity.
        [ForeignKey("ChangeLogId")]
        public virtual ChangeLog ChangeLog { get; set; }
        public long ChangeLogId { get; set; }

        // The type of the entity that was changed.
        public string EntityType { get; set; }
        // The primary key of the entity that was changed.
        public string EntityKey { get; set; }
        // What the change was.
        public string Action { get; set; }

        // A function to get the entity key.
        // This is needed since we record the change before saving, but the primary key of new
        // entities are only available after saving.
        [NotMapped]
        public Func<string> EntityKeyGetter { get; set; }

        // The properties that were changed on this entity.
        public virtual ICollection<ChangeLogProperty> ChangeLogProperties { get; set; }
    }

    /// <summary>
    /// Property that was changed.
    /// </summary>
    public class ChangeLogProperty : IDbEntity
    {
        public long Id { get; set; }
        // The ChangeLogEntity that contains this ChangeLogProperty.
        [ForeignKey("ChangeLogEntityId")]
        public virtual ChangeLogEntity ChangeLogEntity { get; set; }
        public long ChangeLogEntityId { get; set; }

        // The name of the property that was changed.
        public string Name { get; set; }
        // The value of the property before the change.
        public string OriginalValue { get; set; }
        // The value of the property after the change.
        public string CurrentValue { get; set; }
    }

    /// <summary>
    /// Interface to denote that this DbContext has ChangeLogs.
    /// </summary>
    public interface IHasChangeLogs
    {
        // The changes that have been recorded with SaveWithTracking.
        DbSet<ChangeLog> ChangeLogs { get; set; }
    }
}
