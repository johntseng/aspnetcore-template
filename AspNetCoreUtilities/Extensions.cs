using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace AspNetCoreUtilities
{
    /// <summary>
    /// Collection of convenience extension methods.
    /// </summary>
    public static class Extensions
    {
        // Checks whether a string has a value or not.
        // This makes code read a bit more like English.
        // e.g. `if (name.HasValue())` vs `if (!string.IsNullOrWhiteSpace(name))`
        public static bool HasValue(this string s) =>
            !string.IsNullOrWhiteSpace(s);
    }
}
