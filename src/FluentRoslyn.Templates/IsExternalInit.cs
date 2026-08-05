namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill for the marker the compiler requires to emit <c>init</c> accessors, which
/// records use. It ships in .NET 5+ but not in netstandard2.0, and this project must
/// target netstandard2.0 to load into the compiler process.
/// </summary>
/// <remarks>
/// The models here are records on purpose: an incremental generator's caching turns on
/// value equality, and a hand-written class would have to reimplement it.
/// </remarks>
internal static class IsExternalInit
{
}
