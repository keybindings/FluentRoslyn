using System;
using System.Collections.Generic;

namespace FluentRoslyn.Builders;

/// <summary>
/// Orders using directives the conventional way: <c>System</c> and its child namespaces
/// first, then everything else, each group alphabetical.
/// </summary>
internal static class UsingOrder
{
    internal static IComparer<string> Comparer { get; } = new UsingComparer();

    private sealed class UsingComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            var systemX = IsSystem(x);
            if (systemX != IsSystem(y))
                return systemX ? -1 : 1;

            return string.CompareOrdinal(x, y);
        }

        private static bool IsSystem(string name)
            => name == "System" || name.StartsWith("System.", StringComparison.Ordinal);
    }
}
