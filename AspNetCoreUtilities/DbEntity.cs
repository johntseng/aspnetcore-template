using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AspNetCoreUtilities
{
    public abstract class DbEntity
    {
        public long Id { get; set; }
    }

    public abstract class DbSoftDeleteEntity :DbEntity
    {
        public bool IsDeleted { get; set; }
    }

    public static class DbEntityExtensions
    {
        public static IQueryable<T> NotDeleted<T>(this IQueryable<T> queryable)
            where T : DbSoftDeleteEntity =>
            queryable.Where(e => !e.IsDeleted);
    }
}
