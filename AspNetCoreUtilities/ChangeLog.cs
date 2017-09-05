using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace AspNetCoreUtilities
{
    public class ChangeLog : IDbEntity
    {
        public long Id { get; set; }
        public DateTime Time { get; set; }

        public virtual ICollection<ChangeLogEntity> ChangeLogEntities { get; set; }
        
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

        public static int SaveWithTracking<T>(T context, Func<int> saveChanges)
            where T : DbContext, IHasChangeLogs => 
            SaveWithTracking(context,() => Task.FromResult(saveChanges())).Result;
    }

    public class ChangeLogEntity : IDbEntity
    {
        public long Id { get; set; }
        public long ChangeLogId { get; set; }
        [ForeignKey("ChangeLogId")]
        public virtual ChangeLog ChangeLog { get; set; }

        public string EntityType { get; set; }
        public string EntityKey { get; set; }
        public string Action { get; set; }

        [NotMapped]
        public Func<string> EntityKeyGetter { get; set; }

        public virtual ICollection<ChangeLogProperty> ChangeLogProperties { get; set; }
    }

    public class ChangeLogProperty : IDbEntity
    {
        public long Id { get; set; }
        public long ChangeLogEntityId { get; set; }
        [ForeignKey("ChangeLogEntityId")]
        public virtual ChangeLogEntity ChangeLogEntity { get; set; }

        public string Name { get; set; }
        public string OriginalValue { get; set; }
        public string CurrentValue { get; set; }
    }

    public interface IHasChangeLogs
    {
        DbSet<ChangeLog> ChangeLogs { get; set; }
    }
}
