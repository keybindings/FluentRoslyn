using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

/// <summary>
/// Builds an enum declaration. Obtained from <see cref="NamespaceBuilder.Enum(string)"/>.
/// </summary>
public class EnumBuilder : TypeDeclarationBuilder
{
    private static readonly HashSet<Type> IntegralTypes =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
    ];

    private readonly List<(string Name, long? Value, ExpressionSyntax? Raw)> _members = [];
    private TypeNameBuilder? _underlyingType;
    private Type? _underlyingClrType;

    internal EnumBuilder(NamespaceBuilder @namespace, string name) : base(@namespace, name)
    {
    }

    #region FluentMethods

    /// <summary>Sets the enum's accessibility. Public by default.</summary>
    public EnumBuilder WithAccessModifier(AccessModifier accessModifier) => this.With(() => AccessModifier = accessModifier);

    /// <summary>Emits a block-scoped namespace instead of the default file-scoped form.</summary>
    public EnumBuilder BlockScopedNamespace() => this.With(() => IsFileScopedNamespace = false);

    /// <summary>Sets the underlying integral type: <c>enum Name : byte</c>.</summary>
    public EnumBuilder WithUnderlyingType<T>() => this.With(() =>
    {
        if (!IntegralTypes.Contains(typeof(T)))
            throw new ArgumentException($"Enum underlying type must be an integral type, not '{typeof(T)}'.", nameof(T));
        _underlyingType = TypeNameBuilder.New<T>();
        _underlyingClrType = typeof(T);
    });

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("Flags")</c>.</summary>
    public EnumBuilder WithAttribute(string attribute) => this.With(() => AddAttribute(attribute));

    /// <summary>Adds a member with an implicit value: <c>Name</c>.</summary>
    public EnumBuilder AddMember(string name) => this.With(() => _members.Add((RequireName(name), null, null)));

    /// <summary>Adds a member with an explicit value: <c>Name = value</c>.</summary>
    public EnumBuilder AddMember(string name, long value) => this.With(() => _members.Add((RequireName(name), value, null)));

    /// <summary>
    /// Adds a member whose value is a raw constant expression, e.g.
    /// <c>AddMember("All", "0xFFFFFFFFFFFFFFFF")</c> or <c>AddMember("Flag", "1 &lt;&lt; 20")</c> —
    /// the escape hatch for values a <see cref="long"/> cannot express.
    /// </summary>
    public EnumBuilder AddMember(string name, string valueExpression)
        => this.With(() => _members.Add((RequireName(name), null, SyntaxParse.Expression(valueExpression))));

    #endregion

    private protected override MemberDeclarationSyntax BuildDeclaration()
    {
        ValidateMembers();

        var declaration = EnumDeclaration(Name)
            .WithAttributeLists(BuildAttributeLists())
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier))
            .WithMembers(SeparatedList(_members.Select(BuildMember)));

        return _underlyingType is null
            ? declaration
            : declaration.WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(
                SimpleBaseType(_underlyingType.BuildTypeSyntax()))));
    }

    private static EnumMemberDeclarationSyntax BuildMember((string Name, long? Value, ExpressionSyntax? Raw) member)
    {
        var declaration = EnumMemberDeclaration(Identifier(member.Name));

        if (member.Raw is { } raw)
            return declaration.WithEqualsValue(EqualsValueClause(raw));

        if (member.Value is not long value)
            return declaration;

        // Emit a plain integer literal (no L suffix) regardless of the underlying type.
        var literal = LiteralExpression(SyntaxKind.NumericLiteralExpression,
            Literal(value.ToString(CultureInfo.InvariantCulture), value));

        return declaration.WithEqualsValue(EqualsValueClause(literal));
    }

    // Member names must be unique, and any explicit value must fit the underlying type
    // (default int). Deferred to build time because WithUnderlyingType may be called
    // after the members.
    private void ValidateMembers()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, value, _) in _members)
        {
            if (!seen.Add(name))
                throw new InvalidOperationException($"Enum '{Name}' has a duplicate member '{name}'.");

            if (value is long v && !FitsUnderlyingType(v))
                throw new InvalidOperationException(
                    $"Enum '{Name}' member '{name}' value {v} is out of range for underlying type '{(_underlyingClrType ?? typeof(int)).Name}'.");
        }
    }

    private bool FitsUnderlyingType(long value)
        => Type.GetTypeCode(_underlyingClrType ?? typeof(int)) switch
        {
            TypeCode.SByte => value >= sbyte.MinValue && value <= sbyte.MaxValue,
            TypeCode.Byte => value >= byte.MinValue && value <= byte.MaxValue,
            TypeCode.Int16 => value >= short.MinValue && value <= short.MaxValue,
            TypeCode.UInt16 => value >= ushort.MinValue && value <= ushort.MaxValue,
            TypeCode.Int32 => value >= int.MinValue && value <= int.MaxValue,
            TypeCode.UInt32 => value >= uint.MinValue && value <= uint.MaxValue,
            TypeCode.Int64 => true,
            TypeCode.UInt64 => value >= 0,
            _ => true,
        };

    private static string RequireName(string name)
    {
        Identifiers.Validate(name);
        return name;
    }
}
