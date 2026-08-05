using System;
using System.Collections.Immutable;
using System.Linq;
using FluentRoslyn.Templates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FluentRoslyn.UnitTests.Templates;

/// <summary>
/// Covers the meta-generator: it runs on a source-generator project, finds
/// <c>[Template]</c> methods, and lifts each into the FluentRoslyn calls that reproduce
/// it. These drive it through <see cref="CSharpGeneratorDriver"/> so both the lifted
/// source and the diagnostics can be asserted on; the end-to-end proof that the lifted
/// calls actually emit the template lives in the templates example, which CI runs.
/// </summary>
[TestClass]
public class TemplateLifterTests
{
    [TestMethod]
    public void Lifts_TheSignatureAndTheBody()
    {
        var lifted = Lift("""
            using FluentRoslyn.Templates;
            namespace MyGen;
            internal static partial class Templates
            {
                [Template]
                public static int Add(int a, int b) => a + b;
            }
            """);

        lifted.Should()
            .Contain("static partial class Templates").And
            .Contain("EmitAdd(global::FluentRoslyn.Builders.TypeBuilder target)").And
            .Contain("DefineMethod<int>(\"Add\")").And
            .Contain(".WithParameter<int>(\"a\")").And
            .Contain(".WithParameter<int>(\"b\")").And
            .Contain(".AsExpressionBody(@\"a + b\")");
    }

    [TestMethod]
    public void Lifts_AVoidTemplate_ToTheUntypedBuilder()
    {
        var lifted = Lift("""
            using FluentRoslyn.Templates;
            namespace MyGen;
            internal static partial class Templates
            {
                [Template]
                public static void Ping(string host) => System.Console.WriteLine(host);
            }
            """);

        lifted.Should()
            .Contain("global::FluentRoslyn.Builders.MethodBuilder EmitPing").And
            .Contain("DefineMethod(\"Ping\")").And
            .NotContain("DefineMethod<");
    }

    // Measured before this rewrite existed: an unqualified StringBuilder was lifted
    // as-is and failed the *consumer's* build with CS0246, because the generated file
    // carries none of the template file's usings. Binding types here keeps the body
    // self-contained, which is the same choice the builder API makes everywhere.
    [TestMethod]
    public void Lifts_TypesInTheBody_FullyQualified()
    {
        var lifted = Lift("""
            using System.Text;
            using FluentRoslyn.Templates;
            namespace MyGen;
            internal static partial class Templates
            {
                [Template]
                public static string Tag(string value) => new StringBuilder("[").Append(value).ToString();
            }
            """);

        lifted.Should()
            .Contain("new global::System.Text.StringBuilder(").And
            .NotContain("new StringBuilder(");
    }

    // The member name after the dot is not a type, so qualifying it would emit
    // `value.global::…Length`.
    [TestMethod]
    public void Lifts_MemberNames_Untouched()
    {
        var lifted = Lift("""
            using FluentRoslyn.Templates;
            namespace MyGen;
            internal static partial class Templates
            {
                [Template]
                public static int Size(string value) => value.Length;
            }
            """);

        lifted.Should().Contain(".AsExpressionBody(@\"value.Length\")");
    }

    // A template body is arbitrary C# text, which is exactly the input that breaks naive
    // escaping. Asserting on the escaped form would pin the escaping rather than the
    // property that matters, so this reads the literal back and compares it to the
    // original body: what the generator finally emits is this string, unescaped.
    [TestMethod]
    public void Lifts_ABodyContainingQuotes_SoItRoundTrips()
    {
        var lifted = Lift("""
            using FluentRoslyn.Templates;
            namespace MyGen;
            internal static partial class Templates
            {
                [Template]
                public static string Quote(string value) => "\"" + value + "\"";
            }
            """);

        BodyLiteralIn(lifted).Should().Be("""
            "\"" + value + "\""
            """);
    }

    // Parses the lifted source and reads the value of the literal handed to
    // AsExpressionBody, so the assertion is on the text the builder receives.
    private static string BodyLiteralIn(string lifted)
        => CSharpSyntaxTree.ParseText(lifted).GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
            .Where(i => i.Expression.ToString().EndsWith("AsExpressionBody", StringComparison.Ordinal))
            .Select(i => i.ArgumentList.Arguments.Single().Expression)
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax>()
            .Select(l => (string)l.Token.Value!)
            .Single();

    [TestMethod]
    public void ANonStaticTemplate_ReportsFRT001()
    {
        Diagnostics("""
            using FluentRoslyn.Templates;
            namespace MyGen;
            internal static partial class Templates
            {
                [Template]
                public int Add(int a) => a;
            }
            """).Should().Contain("FRT001");
    }

    [TestMethod]
    public void ATemplateInANonPartialType_ReportsFRT002()
    {
        Diagnostics("""
            using FluentRoslyn.Templates;
            namespace MyGen;
            internal static class Templates
            {
                [Template]
                public static int Add(int a) => a;
            }
            """).Should().Contain("FRT002");
    }

    [TestMethod]
    public void ATemplateWithAStatementBody_ReportsFRT003()
    {
        Diagnostics("""
            using FluentRoslyn.Templates;
            namespace MyGen;
            internal static partial class Templates
            {
                [Template]
                public static int Add(int a) { return a; }
            }
            """).Should().Contain("FRT003");
    }

    [TestMethod]
    public void AGenericTemplate_ReportsFRT004()
    {
        Diagnostics("""
            using FluentRoslyn.Templates;
            namespace MyGen;
            internal static partial class Templates
            {
                [Template]
                public static T Echo<T>(T value) => value;
            }
            """).Should().Contain("FRT004");
    }

    // A rejected template must not also be lifted: emitting a half-formed emitter
    // alongside an error would bury the error in follow-on noise.
    [TestMethod]
    public void ARejectedTemplate_IsNotLifted()
    {
        var result = Run("""
            using FluentRoslyn.Templates;
            namespace MyGen;
            internal static partial class Templates
            {
                [Template]
                public static int Add(int a) { return a; }
            }
            """);

        result.GeneratedSources.Should().NotContain(s => s.Contains("EmitAdd"));
    }

    [TestMethod]
    public void NoTemplates_LiftsNothing()
    {
        var result = Run("""
            namespace MyGen;
            internal static class Plain
            {
                public static int Add(int a) => a;
            }
            """);

        result.Diagnostics.Should().BeEmpty();
        // Only the injected attribute.
        result.GeneratedSources.Should().ContainSingle(s => s.Contains("class TemplateAttribute"));
    }

    private static string Lift(string source)
    {
        var result = Run(source);

        result.Diagnostics.Should().BeEmpty("the template should be liftable");

        return result.GeneratedSources.Single(s => s.Contains("static partial class"));
    }

    private static ImmutableArray<string> Diagnostics(string source)
        => Run(source).Diagnostics;

    private static (ImmutableArray<string> Diagnostics, ImmutableArray<string> GeneratedSources) Run(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestGeneratorProject",
            [CSharpSyntaxTree.ParseText(source)],
            Net.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(new TemplateLifter())
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var result = driver.GetRunResult();

        return (
            result.Diagnostics.Select(d => d.Id).ToImmutableArray(),
            result.GeneratedTrees.Select(t => t.ToString()).ToImmutableArray());
    }

    private static class Net
    {
        // The template compilations reference only the BCL: the lifter reads syntax and
        // symbols, and never needs FluentRoslyn itself, which is what makes the emitted
        // calls plain text rather than something it has to bind.
        internal static readonly ImmutableArray<MetadataReference> References =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Text.StringBuilder).Assembly.Location),
            MetadataReference.CreateFromFile(
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                    "System.Runtime.dll")),
        ];
    }
}
