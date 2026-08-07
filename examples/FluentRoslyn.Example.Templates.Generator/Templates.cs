// [Template] is injected as source by the meta-generator package, so nothing has to be
// referenced at run time and nothing flows to the consumer.
using System.Text;
using FluentRoslyn.Templates;

namespace FluentRoslyn.Example.Templates.Generator;

/// <summary>
/// The templates. Every body below is real C#: the compiler checks it, IntelliSense
/// completes it, and Rename refactors it. Rename <c>a</c> in <see cref="Add"/> and this
/// project stops compiling — which is the whole point, because the alternative is a
/// string that nothing checks until it reaches a consumer's build as a CS8785 warning.
/// </summary>
/// <remarks>
/// <c>static partial</c> is required: the meta-generator emits an <c>Emit…</c> method
/// per template into the other half of this class. The templates themselves are never
/// invoked — they exist to be compiled and lifted.
/// </remarks>
internal static partial class Templates
{
    [Template]
    public static int Add(int a, int b) => a + b;

    [Template]
    public static string Describe(string name, int count) => $"{name} has {count} item(s)";

    [Template]
    public static bool IsEmpty(string value) => string.IsNullOrWhiteSpace(value);

    // StringBuilder resolves through this file's `using System.Text;`, which the
    // generated file does not have. The lifter binds it and emits the fully qualified
    // name, so the body stays self-contained wherever it lands.
    [Template]
    public static string Tag(string value) => new StringBuilder("[").Append(value).Append(']').ToString();

    // Deliberately void, and deliberately uncalled. The lifter has two emission
    // branches -- value-returning templates use MethodBuilder<T> and DefineMethod<T>,
    // void ones the non-generic MethodBuilder and DefineMethod(string) -- and every
    // template above returns a value, so without this the void branch's emitted names
    // were compiled by nothing and a rename there would surface only in a consumer's
    // build (review finding R2-15). The generated EmitLog is compiled whether or not
    // anything calls it, which is the whole guard.
    [Template]
    public static void Log(string message) => System.Console.WriteLine(message);
}
