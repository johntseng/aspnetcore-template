using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace AspNetCoreUtilities
{
    public static class Extensions
    {
        public static bool HasValue(this string s) =>
            !string.IsNullOrWhiteSpace(s);
    }
}
