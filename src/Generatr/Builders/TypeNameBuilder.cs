using System;
using System.Collections.Generic;
using System.Linq;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

public class TypeNameBuilder : NamedBuilder
{
    private readonly NamespaceBuilder _namespaceBuilder;
    private readonly SyntaxKind? _predefinedKind;
    private readonly TypeNameBuilder? _declaringType;
    private readonly TypeNameBuilder? _elementType;
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

    public static TypeNameBuilder New<T>()
        => New(typeof(T));

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

        return _namespaceBuilder.IsGlobal
            ? simple
            : QualifiedName(_namespaceBuilder.BuildNameSyntax(), simple);
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
