using System;

namespace Generatr.Extensions;

internal static class ObjectExtensions
{
    public static bool Or(this bool @ref, Func<bool> func) => @ref || func();

    public static T With<T>(this T @ref, Action<T> action)
    {
        action(@ref);
        return @ref;
    }

    public static void IfThen(this bool @ref, Action action)
    {
        if (@ref)
        {
            action();
        }
    }
}