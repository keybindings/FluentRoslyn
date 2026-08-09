using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// A type reference, resolved from a CLR <see cref="Type"/>. Handles arrays, nested
/// types, generics (including open definitions), and the built-in keyword shorthands
/// (<c>int</c> rather than <c>System.Int32</c>). Names are emitted fully qualified.
/// </summary>
public class TypeNameBuilder : NamedBuilder
{
    private readonly NamespaceBuilder _namespaceBuilder;
    private readonly SyntaxKind? _predefinedKind;
    private readonly TypeNameBuilder? _declaringType;
    private readonly TypeNameBuilder? _elementType;
    private readonly TypeDeclarationBuilder? _builderTarget;
    private readonly TypeSyntax? _rawTypeSyntax;
    private readonly int _arrayRank;
    private readonly int _unboundArity;
    private readonly List<TypeNameBuilder> _genericTypes = [];

    private TypeNameBuilder(
        string name,
        NamespaceBuilder namespaceBuilder,
        SyntaxKind? predefinedKind = null,
        TypeNameBuilder? declaringType = null,
        TypeNameBuilder? elementType = null,
        int arrayRank = 0,
        int unboundArity = 0) : base(name, NameValidation)
    {
        _namespaceBuilder = namespaceBuilder;
        _predefinedKind = predefinedKind;
        _declaringType = declaringType;
        _elementType = elementType;
        _arrayRank = arrayRank;
        _unboundArity = unboundArity;
    }

    private TypeNameBuilder(TypeDeclarationBuilder builderTarget) : base(builderTarget.Name, NameValidation)
    {
        _namespaceBuilder = NamespaceBuilder.None;
        _builderTarget = builderTarget;
    }

    // A parsed type name goes through untouched: it is already whatever the caller
    // meant, and the name-validation the other paths apply would reject the qualified
    // and generic forms this exists to carry.
    private TypeNameBuilder(TypeSyntax rawTypeSyntax) : base(rawTypeSyntax.ToString(), _ => { })
    {
        _namespaceBuilder = NamespaceBuilder.None;
        _rawTypeSyntax = rawTypeSyntax;
    }

    /// <summary>
    /// Creates a type reference from a raw type name — for a type the generator cannot
    /// name as <c>T</c>, above all one discovered from the consumer's compilation as an
    /// <c>ISymbol</c>. Parsed, so a malformed name is rejected rather than emitted.
    /// </summary>
    /// <remarks>
    /// Unlike the <c>T</c> and builder-reference paths, the result is not annotated for
    /// the simplifier: the text is taken as written, so a caller passing a fully
    /// qualified name gets one. That matches <c>Returns(string)</c> and
    /// <c>DefineEvent(name, handlerTypeName)</c>, the raw-name escape hatches that came
    /// before it.
    /// </remarks>
    internal static TypeNameBuilder ForRawName(string typeName)
        => new(SyntaxParse.TypeName(typeName));

    /// <summary>
    /// Creates a type reference to a type being built alongside — the definition and
    /// every reference share one name. Resolution is lazy, so the guard against
    /// referencing a generic type builder holds regardless of call order.
    /// </summary>
    internal static TypeNameBuilder For(TypeDeclarationBuilder target)
        => new(target ?? throw new ArgumentNullException(nameof(target)));

    /// <summary>Creates a type reference for <typeparamref name="T"/>.</summary>
    public static TypeNameBuilder New<T>()
        => New(typeof(T));

    /// <summary>Creates a type reference for the given runtime type.</summary>
    public static TypeNameBuilder New(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        var tb = NewEmptyBuilder(type);

        foreach (var genericArg in GenericArgumentsOf(type))
        {
            tb._genericTypes.Add(New(genericArg));
        }

        return tb;
    }

    internal TypeSyntax BuildTypeSyntax()
    {
        if (_rawTypeSyntax is not null)
            return _rawTypeSyntax;

        // The generic guard used to be repeated here, checking the leaf builder only.
        // It now lives on BuildTypeSyntax itself and walks the declaring chain, so this
        // path and the ones that never came through here are guarded by the same code.
        if (_builderTarget is not null)
            return _builderTarget.BuildTypeSyntax();

        if (_elementType is not null)
            return ArrayType(_elementType.BuildTypeSyntax())
                .WithRankSpecifiers(SingletonList(ArrayRankSpecifier(
                    SeparatedList(Enumerable.Repeat<ExpressionSyntax>(OmittedArraySizeExpression(), _arrayRank)))));

        if (_predefinedKind is { } kind)
            return PredefinedType(Token(kind));

        return Qualify(BuildSimpleName());
    }

    internal override SyntaxNode BuildSyntax() => BuildTypeSyntax();

    private SimpleNameSyntax BuildSimpleName()
    {
        if (_unboundArity > 0)
            return GenericName(Identifier(Name),
                TypeArgumentList(SeparatedList(
                    Enumerable.Repeat<TypeSyntax>(OmittedTypeArgument(), _unboundArity))));

        return _genericTypes.Count == 0
            ? IdentifierName(Name)
            : GenericName(Identifier(Name),
                TypeArgumentList(SeparatedList(_genericTypes.Select(t => t.BuildTypeSyntax()))));
    }

    // A nested type is qualified by its declaring type; only a top-level type is
    // qualified by its namespace.
    private TypeSyntax Qualify(SimpleNameSyntax simple)
    {
        if (_declaringType is not null)
            return QualifiedName((NameSyntax)_declaringType.BuildTypeSyntax(), simple);

        if (_namespaceBuilder.IsGlobal)
            return simple;

        // Annotated so the simplifier can shorten this to `simple` and import the
        // namespace, which a syntax-only pass could not work out on its own.
        return QualifiedName(_namespaceBuilder.BuildNameSyntax(), simple)
            .WithAdditionalAnnotations(TypeNameSimplifier.Annotation(_namespaceBuilder.ToString()));
    }

    private static TypeNameBuilder NewEmptyBuilder(Type type)
    {
        if (type.IsArray)
        {
            var elementType = type.GetElementType()
                              ?? throw new InvalidOperationException($"Array type '{type}' has no element type.");

            return new TypeNameBuilder(
                type.Name,
                NamespaceBuilder.None,
                elementType: New(elementType),
                arrayRank: type.GetArrayRank());
        }

        // A placeholder resolves to its declared emitted name, not to reflection: the
        // CLR type only exists so references to a generated type can be compile-checked.
        if (TryUsePlaceholderName(type, out var placeholder))
            return placeholder;

        if (TryUseShorthand(type, out var shortName, out var kind))
        {
            return new TypeNameBuilder(shortName, NamespaceBuilder.None, kind);
        }

        var typeName = new string(type.Name.TakeWhile(c => c != '`').ToArray());

        // An unbound generic definition (typeof(List<>)) has no type arguments to recurse
        // into, so its arity has to be carried across explicitly or it emits as "List".
        var unboundArity = type.IsGenericTypeDefinition ? type.GetGenericArguments().Length : 0;

        // Nested types report the enclosing namespace too, so qualifying by both the
        // declaring type and the namespace would double it up.
        if (DeclaringTypeOf(type) is { } declaringType)
        {
            return new TypeNameBuilder(
                typeName,
                NamespaceBuilder.None,
                declaringType: New(declaringType),
                unboundArity: unboundArity);
        }

        return new TypeNameBuilder(typeName, NamespaceOf(type), unboundArity: unboundArity);
    }

    // Types in the global namespace report a null namespace.
    private static NamespaceBuilder NamespaceOf(Type type)
        => string.IsNullOrEmpty(type.Namespace)
            ? NamespaceBuilder.None
            : NamespaceBuilder.Get(type.Namespace!);

    // Reflection hands back the OPEN declaring type for a closed nested type
    // (Outer&lt;int&gt;.Inner reports Outer&lt;&gt;), so it has to be re-closed over the
    // arguments the nested type inherited from it.
    private static Type? DeclaringTypeOf(Type type)
    {
        if (!type.IsNested || type.DeclaringType is not { } declaringType)
            return null;

        var inheritedCount = InheritedArgumentCount(declaringType);

        if (inheritedCount == 0 || !declaringType.IsGenericTypeDefinition)
            return declaringType;

        var inheritedArgs = type.GenericTypeArguments.Take(inheritedCount).ToArray();

        return inheritedArgs.Length == inheritedCount
            ? declaringType.MakeGenericType(inheritedArgs)
            : declaringType;
    }

    // A nested generic type reports its declaring type's arguments alongside its own;
    // only the trailing ones belong to this type.
    private static IEnumerable<Type> GenericArgumentsOf(Type type)
    {
        if (type.IsArray || type.IsGenericTypeDefinition)
            return [];

        var args = type.GenericTypeArguments;

        if (!type.IsNested || type.DeclaringType is not { } declaringType)
            return args;

        return args.Skip(InheritedArgumentCount(declaringType));
    }

    private static int InheritedArgumentCount(Type declaringType)
        => declaringType.IsGenericType ? declaringType.GetGenericArguments().Length : 0;

    private static bool TryUsePlaceholderName(Type type, out TypeNameBuilder placeholder)
    {
        placeholder = null!;

        if (type.GetCustomAttribute<EmitsAsAttribute>() is not { } emitsAs)
            return false;

        // A generic placeholder has no way to say what its emitted arity or argument
        // names are, so it cannot be mapped faithfully.
        if (type.IsGenericType)
            throw new InvalidOperationException(
                $"Placeholder '{type.Name}' is generic, which [EmitsAs] does not support. " +
                "Use a non-generic placeholder per constructed type, or the raw-string overloads.");

        placeholder = ForEmittedName(emitsAs.FullTypeName, $"[EmitsAs] on '{type.Name}'");
        return true;
    }

    /// <summary>
    /// Splits an emitted type name into a validated namespace, declaring types, and simple
    /// name. Dots separate namespace levels; <c>+</c> is the CLR nesting marker and
    /// separates a declaring type from the type nested in it.
    /// </summary>
    /// <remarks>
    /// The marker is load-bearing, not decoration. A dotted name is split at the last dot,
    /// so <c>MyApp.Outer.Inner</c> records <c>MyApp.Outer</c> as a namespace — which emits
    /// correctly, and then imports as <c>using MyApp.Outer;</c> the moment
    /// <c>SimplifyTypeNames</c> is turned on, for a namespace that does not exist. Nothing
    /// in the string says which segments are namespaces, so the author has to; the
    /// alternative was to guess, and this library does not guess about type identity.
    /// </remarks>
    internal static TypeNameBuilder ForEmittedName(string fullTypeName, string context)
    {
        if (string.IsNullOrWhiteSpace(fullTypeName))
            throw new ArgumentException($"{context} names an empty type.", nameof(fullTypeName));

        if (fullTypeName.IndexOfAny(['<', '>', '`', '[', ']']) >= 0)
            throw new ArgumentException(
                $"{context} names '{fullTypeName}', which is not a plain namespace-qualified " +
                "identifier. Generics and arrays are not supported here.",
                nameof(fullTypeName));

        var nesting = fullTypeName.Split('+');
        if (nesting.Any(part => part.Length == 0))
            throw new ArgumentException(
                $"{context} names '{fullTypeName}', which has an empty segment beside a '+' " +
                "nesting marker. Write the declaring type and the nested type either side of it.",
                nameof(fullTypeName));

        var type = ForQualifiedName(nesting[0]);

        for (var i = 1; i < nesting.Length; i++)
        {
            Identifiers.Validate(nesting[i]);
            type = new TypeNameBuilder(nesting[i], NamespaceBuilder.None, declaringType: type);
        }

        return type;
    }

    private static TypeNameBuilder ForQualifiedName(string qualifiedName)
    {
        var lastDot = qualifiedName.LastIndexOf('.');
        var simpleName = lastDot < 0 ? qualifiedName : qualifiedName.Substring(lastDot + 1);
        Identifiers.Validate(simpleName);

        var @namespace = lastDot < 0
            ? NamespaceBuilder.None
            : NamespaceBuilder.Get(qualifiedName.Substring(0, lastDot));

        return new TypeNameBuilder(simpleName, @namespace);
    }

    private static bool TryUseShorthand(Type type, out string shortName, out SyntaxKind kind)
    {
        shortName = string.Empty;
        kind = default;

        if (type.Namespace != "System" || type.IsNested)
            return false;

        (shortName, kind) = type.Name switch
        {
            "String" => ("string", SyntaxKind.StringKeyword),
            "Boolean" => ("bool", SyntaxKind.BoolKeyword),
            "Object" => ("object", SyntaxKind.ObjectKeyword),
            "Char" => ("char", SyntaxKind.CharKeyword),
            "SByte" => ("sbyte", SyntaxKind.SByteKeyword),
            "Byte" => ("byte", SyntaxKind.ByteKeyword),
            "Int16" => ("short", SyntaxKind.ShortKeyword),
            "UInt16" => ("ushort", SyntaxKind.UShortKeyword),
            "Int32" => ("int", SyntaxKind.IntKeyword),
            "UInt32" => ("uint", SyntaxKind.UIntKeyword),
            "Int64" => ("long", SyntaxKind.LongKeyword),
            "UInt64" => ("ulong", SyntaxKind.ULongKeyword),
            "Single" => ("float", SyntaxKind.FloatKeyword),
            "Double" => ("double", SyntaxKind.DoubleKeyword),
            "Decimal" => ("decimal", SyntaxKind.DecimalKeyword),
            _ => (string.Empty, default(SyntaxKind))
        };

        return shortName != string.Empty;
    }

    private static void NameValidation(string name)
    {

    }
}
