using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// Builds a positional record: <c>public record Person(string Name, int Age);</c>.
/// </summary>
public class RecordBuilder : TypeDeclarationBuilder
{
    private readonly List<IParameter> _params = [];
    private readonly List<TypeSyntax> _interfaces = [];
    private TypeSyntax? _baseTypeName;
    private RecordBuilder? _baseRecord;
    private string[] _baseArguments = [];
    private readonly GenericParameters _generics = new();

    internal override bool HasTypeParameters => _generics.Any;
    private readonly OperatorSet _operators = new();
    private bool _isStruct;

    internal RecordBuilder(SourceFile file, string name, TypeDeclarationBuilder? declaringType = null) : base(file, name, declaringType)
    {
    }

    #region FluentMethods

    /// <summary>Sets the record's accessibility. Public by default.</summary>
    public RecordBuilder WithAccessModifier(AccessModifier accessModifier) => this.With(() => AccessModifier = accessModifier);

    /// <summary>Documents the record with an XML <c>&lt;summary&gt;</c>.</summary>
    public RecordBuilder WithSummary(string text) => this.With(() => AddSummary(text));

    /// <summary>Emits <c>record struct</c> rather than <c>record</c> (a record class).</summary>
    public RecordBuilder AsStruct() => this.With(() => _isStruct = true);

    /// <summary>Adds a positional parameter, e.g. <c>WithParameter&lt;string&gt;("Name")</c>.</summary>
    public RecordBuilder WithParameter<T>(string name) => this.With(() => _params.Add(Parameter<T>.New(name)));

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("Serializable")</c>.</summary>
    public RecordBuilder WithAttribute(string attribute) => this.With(() => AddAttribute(attribute));

    /// <summary>
    /// Inherits from a base record, forwarding the given arguments to its primary
    /// constructor: <c>record Derived(int X) : Base(X)</c>. The base is emitted before any
    /// interfaces, as C# requires.
    /// </summary>
    /// <remarks>
    /// The base is resolved at emission, not here, so the guard against referencing a
    /// generic type builder holds regardless of call order — the same bargain
    /// <c>TypeNameBuilder.For</c> strikes, and the reason it is order-proof there.
    /// </remarks>
    public RecordBuilder WithParent(RecordBuilder parent, params string[] arguments)
    {
        if (parent is null) throw new ArgumentNullException(nameof(parent));
        return SetParent(null, parent, arguments);
    }

    /// <summary>
    /// Inherits from a base record named by a raw type name, forwarding the given
    /// arguments to its primary constructor.
    /// </summary>
    public RecordBuilder WithParent(string typeName, params string[] arguments)
        => SetParent(SyntaxParse.TypeName(typeName), null, arguments);

    private RecordBuilder SetParent(TypeSyntax? typeName, RecordBuilder? record, string[] arguments)
        => this.With(() =>
        {
            _baseTypeName = typeName;
            _baseRecord = record;
            _baseArguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        });

    /// <summary>Adds an implemented interface from a raw name, e.g. <c>WithInterface("IEquatable&lt;Person&gt;")</c>.</summary>
    public RecordBuilder WithInterface(string interfaceName)
        => this.With(() => _interfaces.Add(SyntaxParse.TypeName(interfaceName)));

    /// <summary>Adds an implemented interface from a type, e.g. <c>WithInterface&lt;IDisposable&gt;()</c>.</summary>
    public RecordBuilder WithInterface<TInterface>()
        => this.With(() => _interfaces.Add(TypeNameBuilder.New<TInterface>().BuildTypeSyntax()));

    /// <summary>Adds a generic type parameter, e.g. <c>WithTypeParameter("T")</c> for <c>Name&lt;T&gt;</c>.</summary>
    public RecordBuilder WithTypeParameter(string name)
        => this.With(() => _generics.AddTypeParameter(name));

    /// <summary>Constrains a type parameter, e.g. <c>WithConstraint("T", "class")</c>.</summary>
    public RecordBuilder WithConstraint(string typeParameter, string constraint)
        => this.With(() => _generics.AddConstraint(typeParameter, constraint));

    /// <summary>
    /// Declares an operator returning <typeparamref name="TReturn"/> on the record. The
    /// record gains a brace body to hold it, after the positional parameter list.
    /// </summary>
    /// <remarks>
    /// <c>==</c> and <c>!=</c> are refused: a record synthesizes both from its value
    /// semantics, and declaring either is an error in the consumer's build. If the
    /// synthesized equality is wrong for the type, the member to replace is
    /// <c>Equals(T)</c>, not the operators.
    /// </remarks>
    /// <typeparam name="TReturn">The operator's result type.</typeparam>
    /// <param name="kind">Which operator to declare.</param>
    /// <returns>The operator builder.</returns>
    public OperatorBuilder<TReturn> DefineOperator<TReturn>(OperatorKind kind)
        => AddOperator(new OperatorBuilder<TReturn>(RefuseSynthesized(kind)));

    /// <summary>Declares an operator whose result type is named by text.</summary>
    /// <param name="kind">Which operator to declare.</param>
    /// <param name="resultTypeName">The result type, as C# text.</param>
    /// <returns>The operator builder.</returns>
    public OperatorBuilder DefineOperator(OperatorKind kind, string resultTypeName)
        => AddOperator(new OperatorBuilder(RefuseSynthesized(kind), SyntaxParse.TypeName(resultTypeName)));

    /// <summary>Declares a conversion to <typeparamref name="TTarget"/> on the record.</summary>
    /// <typeparam name="TTarget">The type converted to.</typeparam>
    /// <param name="kind">Whether the conversion is implicit or explicit.</param>
    /// <returns>The operator builder.</returns>
    public OperatorBuilder<TTarget> DefineConversion<TTarget>(ConversionKind kind)
        => AddOperator(new OperatorBuilder<TTarget>(kind));

    /// <summary>Declares a conversion to a type named by text.</summary>
    /// <param name="kind">Whether the conversion is implicit or explicit.</param>
    /// <param name="targetTypeName">The type converted to, as C# text.</param>
    /// <returns>The operator builder.</returns>
    public OperatorBuilder DefineConversion(ConversionKind kind, string targetTypeName)
        => AddOperator(new OperatorBuilder(kind, SyntaxParse.TypeName(targetTypeName)));

    #endregion

    private TOperator AddOperator<TOperator>(TOperator @operator) where TOperator : IOperatorMember
        => _operators.Add(@operator);

    private OperatorKind RefuseSynthesized(OperatorKind kind)
        => kind is OperatorKind.Equality or OperatorKind.Inequality
            ? throw new InvalidOperationException(
                $"Record '{Name}' cannot declare operator '{Operators.SymbolFor(kind)}': a record " +
                "synthesizes == and != from its value semantics, and declaring either is an error. " +
                "To change what equality means, declare Equals instead.")
            : kind;

    private protected override MemberDeclarationSyntax BuildDeclaration()
    {
        var kind = _isStruct ? SyntaxKind.RecordStructDeclaration : SyntaxKind.RecordDeclaration;

        var declaration = RecordDeclaration(kind, Token(SyntaxKind.RecordKeyword), Identifier(Name))
            .WithAttributeLists(BuildAttributeLists())
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier))
            .WithParameterList(SyntaxParameters.List(_params));

        // A bodiless record ends in a semicolon; one with members gains braces after the
        // positional parameter list. Records are never static, so isStaticType is false.
        _operators.Validate(Name, isStaticType: false);
        var operators = new List<MemberDeclarationSyntax>();
        _operators.AppendMembers(operators);

        declaration = operators.Count == 0
            ? declaration.WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
            : declaration
                .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
                .WithMembers(List(operators))
                .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken));

        declaration = _generics.ApplyTo(declaration, $"Record '{Name}'");

        if (BuildBaseList() is { } baseList)
            declaration = declaration.WithBaseList(baseList);

        // A record struct carries an explicit `struct` keyword; a record class carries none.
        return _isStruct
            ? declaration.WithClassOrStructKeyword(Token(SyntaxKind.StructKeyword))
            : declaration;
    }

    // The base record comes first and, unlike an interface, carries an argument list
    // forwarding to its primary constructor — so it needs PrimaryConstructorBaseType
    // rather than the SimpleBaseType that SyntaxBaseList produces.
    private BaseListSyntax? BuildBaseList()
    {
        var baseType = _baseTypeName ?? _baseRecord?.BuildTypeSyntax();

        if (baseType is null)
            return SyntaxBaseList.From(_interfaces);

        if (_isStruct)
            throw new InvalidOperationException($"Record struct '{Name}' cannot inherit from a base record.");

        var arguments = ArgumentList(SeparatedList(
            _baseArguments.Select(a => Argument(SyntaxParse.Expression(a)))));

        var baseTypes = new List<BaseTypeSyntax> { PrimaryConstructorBaseType(baseType, arguments) };
        baseTypes.AddRange(_interfaces.Select(i => (BaseTypeSyntax)SimpleBaseType(i)));

        return BaseList(SeparatedList(baseTypes));
    }
}
