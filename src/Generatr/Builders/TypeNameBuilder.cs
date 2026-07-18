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
    private readonly List<TypeNameBuilder> _genericTypes = [];

    private TypeNameBuilder(string name, NamespaceBuilder namespaceBuilder, SyntaxKind? predefinedKind = null) : base(name, NameValidation)
    {
        _namespaceBuilder = namespaceBuilder;
        _predefinedKind = predefinedKind;
    }

    public static TypeNameBuilder New<T>()
        => New(typeof(T));

    private static TypeNameBuilder New(Type type)
    {
        var tb = NewEmptyBuilder(type);

        foreach (var genericArg in type.GenericTypeArguments)
        {
            tb._genericTypes.Add(New(genericArg));
        }

        return tb;
    }

    internal TypeSyntax BuildTypeSyntax()
    {
        if (_predefinedKind is { } kind)
            return PredefinedType(Token(kind));

        SimpleNameSyntax simple = _genericTypes.Count == 0
            ? IdentifierName(Name)
            : GenericName(Identifier(Name),
                TypeArgumentList(SeparatedList(_genericTypes.Select(t => t.BuildTypeSyntax()))));

        return _namespaceBuilder == NamespaceBuilder.None
            ? simple
            : QualifiedName(_namespaceBuilder.BuildNameSyntax(), simple);
    }

    internal override SyntaxNode BuildSyntax() => BuildTypeSyntax();

    private static TypeNameBuilder NewEmptyBuilder(Type type)
    {
        if (TryUseShorthand(type, out var shortName, out var kind))
        {
            return new TypeNameBuilder(shortName, NamespaceBuilder.None, kind);
        }

        var typeName = new string(type.Name.TakeWhile(c => c != '`').ToArray());

        return new TypeNameBuilder(typeName, NamespaceBuilder.Get(type.Namespace));
    }

    private static bool TryUseShorthand(Type type, out string shortName, out SyntaxKind kind)
    {
        shortName = string.Empty;
        kind = default;

        if (type.Namespace != "System")
            return false;

        (shortName, kind) = type.Name switch
        {
            "String" => ("string", SyntaxKind.StringKeyword),
            "Boolean" => ("bool", SyntaxKind.BoolKeyword),
            "Int32" => ("int", SyntaxKind.IntKeyword),
            "Double" => ("double", SyntaxKind.DoubleKeyword),
            "Single" => ("float", SyntaxKind.FloatKeyword),
            _ => (string.Empty, default(SyntaxKind))
        };

        return shortName != string.Empty;
    }

    private static void NameValidation(string name)
    {

    }
}
