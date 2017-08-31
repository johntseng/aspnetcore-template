using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AspNetCoreUtilities
{
    public interface IDbEntity
    {
        long Id { get; set; }
    }

    public interface ISoftDelete
    {
        bool IsDeleted { get; set; }
    }

    public static class DbEntityExtensions
    {
        public static IQueryable<T> NotDeleted<T>(this IQueryable<T> queryable)
            where T : ISoftDelete =>
            queryable.Where(e => !e.IsDeleted);
    }
}
