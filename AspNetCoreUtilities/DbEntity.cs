using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AspNetCoreUtilities
{
    /// <summary>
    /// Interface to standardize primary keys for all entities.
    /// </summary>
    public interface IDbEntity
    {
        long Id { get; set; }
    }

    /// <summary>
    /// Interface to standardize name for entities that can be soft deleted.
    /// </summary>
    public interface ISoftDelete
    {
        bool IsDeleted { get; set; }
    }

    /// <summary>
    /// Collection of extension methods for database entities.
    /// </summary>
    public static class DbEntityExtensions
    {
        // Query only the entities that have not been deleted.
        public static IQueryable<T> NotDeleted<T>(this IQueryable<T> queryable)
            where T : ISoftDelete =>
            queryable.Where(e => !e.IsDeleted);
    }
}
