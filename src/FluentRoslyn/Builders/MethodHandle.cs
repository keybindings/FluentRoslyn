using System;
using FluentRoslyn.Abstractions;

namespace FluentRoslyn.Builders;

/// <summary>
/// The implementation behind every <c>IMethod</c> handle. Carries only the method's
/// name — the shape was validated when <c>AsCallable</c> created the handle, and the
/// arity lives in the interface's type arguments where the compiler enforces it.
/// </summary>
internal class MethodHandle
{
    internal MethodHandle(string methodName)
    {
        MethodName = methodName;
    }

    internal string MethodName { get; }

    /// <summary>
    /// Recovers the implementation from a handle interface. The interfaces are public
    /// but only <c>AsCallable</c> mints handles; anything else cannot carry a name.
    /// </summary>
    internal static MethodHandle From(object method, string context)
        => method as MethodHandle
           ?? throw new ArgumentException(
               $"{context} was given an IMethod that was not created by AsCallable.", nameof(method));
}

internal sealed class MethodHandle0 : MethodHandle, IMethod
{
    internal MethodHandle0(string methodName) : base(methodName)
    {
    }
}

internal sealed class MethodHandle1<T1> : MethodHandle, IMethod<T1>
{
    internal MethodHandle1(string methodName) : base(methodName)
    {
    }
}

internal sealed class MethodHandle2<T1, T2> : MethodHandle, IMethod<T1, T2>
{
    internal MethodHandle2(string methodName) : base(methodName)
    {
    }
}

internal sealed class MethodHandle3<T1, T2, T3> : MethodHandle, IMethod<T1, T2, T3>
{
    internal MethodHandle3(string methodName) : base(methodName)
    {
    }
}
