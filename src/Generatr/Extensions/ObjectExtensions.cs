using System;

namespace Generatr.Extensions;

internal static class ObjectExtensions
{
    public static T With<T>(this T @ref, Action<T> action)
    {
        action(@ref);
        return @ref;
    }

    public static void Then(this bool @ref, Action action)
    {
        if (@ref)
        {
            action();
        }
    }
}