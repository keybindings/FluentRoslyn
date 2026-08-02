using System;
using System.Collections.Generic;
using System.Linq;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// Builds a method declaration. Obtained from <c>DefineMethod</c> on a type builder.
/// </summary>
public class MethodBuilder : NamedBuilder, IAccessModifier, IMemberSyntaxBuilder
{
    private TypeSyntax _returnType;
    private bool _returnsVoid;
    private readonly List<IParameter> _params = [];
    private readonly List<StatementSyntax> _statements = [];
    private readonly List<AttributeListSyntax> _attributes = [];
    private readonly GenericParameters _generics = new();
    private readonly DocComment _docs = new();
    private ExpressionSyntax? _expressionBody;
    private bool _handleIssued;

    private MethodBuilder(
        string name,
        AccessModifier accessModifier,
        TypeSyntax returnType,
        bool returnsVoid) : base(name, Identifiers.Validate)
    {
        AccessModifier = accessModifier;
        _returnType = returnType;
        _returnsVoid = returnsVoid;
    }

    /// <summary>Whether the method is <c>static</c>.</summary>
    public bool IsStatic { get; set; }

    /// <summary>Whether the method is <c>partial</c>.</summary>
    public bool IsPartial { get; set; }

    /// <summary>
    /// Whether the method is <c>async</c>. The return type must be awaitable —
    /// <c>void</c>, <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>, and so on.
    /// </summary>
    public bool IsAsync { get; set; }

    /// <summary>The inheritance modifier — virtual, abstract, override, or sealed override.</summary>
    public Inheritance Inheritance { get; set; }

    internal bool IsAbstract => Inheritance == Inheritance.Abstract;

    /// <summary>The method's accessibility.</summary>
    public AccessModifier AccessModifier { get; set; }

    /// <summary>A void method: <c>void Name(...) { }</c>. Add parameters with <see cref="WithParameter{T}(string)"/>.</summary>
    internal static MethodBuilder Action(string name, AccessModifier accessModifier)
        => new(name, accessModifier, PredefinedType(Token(SyntaxKind.VoidKeyword)), returnsVoid: true);

    /// <summary>A method returning <paramref name="returnType"/>; requires a body.</summary>
    internal static MethodBuilder Returning(string name, AccessModifier accessModifier, TypeNameBuilder returnType)
        => new(name, accessModifier, returnType.BuildTypeSyntax(), returnsVoid: false);

    #region FluentMethods

    /// <summary>Marks the method <c>static</c>.</summary>
    public MethodBuilder Static() => this.With(() => IsStatic = true);

    /// <summary>Marks the method <c>partial</c> (e.g. a source generator implementing a partial method).</summary>
    public MethodBuilder Partial() => this.With(() => IsPartial = true);

    /// <summary>
    /// Marks the method <c>async</c>. Pair it with an awaitable return type, e.g.
    /// <c>DefineMethod&lt;Task&gt;("SaveAsync").Async()</c>.
    /// </summary>
    public MethodBuilder Async() => this.With(() => IsAsync = true);

    /// <summary>Marks the method <c>virtual</c>.</summary>
    public MethodBuilder Virtual() => this.With(() => Inheritance = Inheritance.Virtual);

    /// <summary>
    /// Marks the method <c>abstract</c>: it emits no body, and the declaring type must
    /// itself be abstract.
    /// </summary>
    public MethodBuilder Abstract() => this.With(() => Inheritance = Inheritance.Abstract);

    /// <summary>Marks the method <c>override</c>.</summary>
    public MethodBuilder Override() => this.With(() => Inheritance = Inheritance.Override);

    /// <summary>Marks the method <c>sealed override</c>.</summary>
    public MethodBuilder SealedOverride() => this.With(() => Inheritance = Inheritance.SealedOverride);

    /// <summary>Sets the method's accessibility.</summary>
    public MethodBuilder WithAccessModifier(AccessModifier accessModifier) => this.With(() => AccessModifier = accessModifier);

    /// <summary>Appends a parameter of type <typeparamref name="T"/>.</summary>
    public MethodBuilder WithParameter<T>(string name) => this.With(() =>
    {
        GuardParametersMutable();
        _params.Add(Parameter<T>.New(name));
    });

    /// <summary>
    /// Appends a parameter of type <typeparamref name="T"/> and hands back a typed
    /// reference to it, for use with <see cref="Assign{TValue}"/>. Returns the builder,
    /// so the fluent chain is unbroken:
    /// <c>.WithParameter&lt;int&gt;("id", out var id)</c>.
    /// </summary>
    public MethodBuilder WithParameter<T>(string name, out IReference<T> reference)
    {
        GuardParametersMutable();
        var parameter = Parameter<T>.New(name);
        _params.Add(parameter);
        reference = new ParameterReference<T>(parameter.Name);
        return this;
    }

    /// <summary>
    /// Appends an assignment statement, e.g. <c>Name = name;</c>. Both sides are
    /// references of the same type, so assigning the wrong one is a compile error in the
    /// generator rather than broken generated source.
    /// </summary>
    public MethodBuilder Assign<TValue>(IReference<TValue> target, IReference<TValue> value)
        => this.With(() => _statements.Add(
            SyntaxReferences.Assignment(target, value, _params, IsStatic, $"Method '{Name}'")));

    /// <summary>
    /// Documents the method with an XML <c>&lt;summary&gt;</c>. Newlines become separate
    /// comment lines, and XML markup characters are escaped.
    /// </summary>
    public MethodBuilder WithSummary(string text) => this.With(() => _docs.SetSummary(text));

    /// <summary>
    /// Documents a parameter: <c>&lt;param name="..."&gt;</c>. The name must match a
    /// parameter added with <see cref="WithParameter{T}(string)"/>.
    /// </summary>
    public MethodBuilder WithParameterDoc(string parameterName, string text)
        => this.With(() => _docs.AddParameter(parameterName, text));

    /// <summary>Documents the return value: <c>&lt;returns&gt;</c>.</summary>
    public MethodBuilder WithReturnsDoc(string text) => this.With(() => _docs.SetReturns(text));

    /// <summary>Adds a generic type parameter, e.g. <c>WithTypeParameter("T")</c> for <c>Name&lt;T&gt;</c>.</summary>
    public MethodBuilder WithTypeParameter(string name) => this.With(() => _generics.AddTypeParameter(name));

    /// <summary>
    /// Sets the return type from a raw type name, e.g. <c>Returns("T")</c> or
    /// <c>Returns("List&lt;T&gt;")</c> — for returning a generic type parameter that is
    /// not a CLR type. Requires a body.
    /// </summary>
    public MethodBuilder Returns(string typeName) => this.With(() =>
    {
        _returnType = SyntaxParse.TypeName(typeName);
        _returnsVoid = false;
    });

    /// <summary>
    /// Sets the return type to a type being generated alongside — the type's builder is
    /// the reference, so the name is spelled once. Requires a body.
    /// </summary>
    public MethodBuilder Returns(TypeDeclarationBuilder type) => this.With(() =>
    {
        _returnType = TypeNameBuilder.For(type).BuildTypeSyntax();
        _returnsVoid = false;
    });

    /// <summary>
    /// Appends a parameter whose type is being generated alongside — the type's builder
    /// is the reference, so the name is spelled once. For a typed handle to the
    /// parameter, give the generated type an <c>[EmitsAs]</c> placeholder and use
    /// <see cref="WithParameter{T}(string, out IReference{T})"/> instead.
    /// </summary>
    public MethodBuilder WithParameter(TypeDeclarationBuilder type, string name)
        => this.With(() =>
        {
            GuardParametersMutable();
            _params.Add(Parameter.Of(type, name));
        });

    /// <summary>
    /// Hands back a typed handle to this parameterless method, for emitting
    /// type-checked calls with <c>Call</c>. Take the handle after the parameters are
    /// declared — the signature freezes once a handle exists.
    /// </summary>
    public MethodBuilder AsCallable(out IMethod method)
    {
        ValidateHandle();
        method = new MethodHandle0(Name);
        return this;
    }

    /// <summary>
    /// Hands back a typed handle to this one-parameter method. The type argument is
    /// validated against the declared parameter, so a handle that exists is a handle
    /// that matches — and a call through it type-checks in the generator.
    /// </summary>
    public MethodBuilder AsCallable<T1>(out IMethod<T1> method)
    {
        ValidateHandle(typeof(T1));
        method = new MethodHandle1<T1>(Name);
        return this;
    }

    /// <summary>Hands back a typed handle to this two-parameter method.</summary>
    public MethodBuilder AsCallable<T1, T2>(out IMethod<T1, T2> method)
    {
        ValidateHandle(typeof(T1), typeof(T2));
        method = new MethodHandle2<T1, T2>(Name);
        return this;
    }

    /// <summary>Hands back a typed handle to this three-parameter method.</summary>
    public MethodBuilder AsCallable<T1, T2, T3>(out IMethod<T1, T2, T3> method)
    {
        ValidateHandle(typeof(T1), typeof(T2), typeof(T3));
        method = new MethodHandle3<T1, T2, T3>(Name);
        return this;
    }

    /// <summary>Appends a call statement: <c>target.Method();</c>.</summary>
    public MethodBuilder Call<TTarget>(IReference<TTarget> target, IMethod method)
        => AddCall(target, method);

    /// <summary>
    /// Appends a call statement: <c>target.Method(argument1);</c>. The argument
    /// reference's type must match the handle's — a mismatch is a compile error in the
    /// generator rather than broken generated source.
    /// </summary>
    public MethodBuilder Call<TTarget, T1>(IReference<TTarget> target, IMethod<T1> method, IReference<T1> argument1)
        => AddCall(target, method, argument1);

    /// <summary>Appends a two-argument call statement.</summary>
    public MethodBuilder Call<TTarget, T1, T2>(
        IReference<TTarget> target, IMethod<T1, T2> method, IReference<T1> argument1, IReference<T2> argument2)
        => AddCall(target, method, argument1, argument2);

    /// <summary>Appends a three-argument call statement.</summary>
    public MethodBuilder Call<TTarget, T1, T2, T3>(
        IReference<TTarget> target, IMethod<T1, T2, T3> method,
        IReference<T1> argument1, IReference<T2> argument2, IReference<T3> argument3)
        => AddCall(target, method, argument1, argument2, argument3);

    private MethodBuilder AddCall(IReference target, object method, params IReference[] arguments)
        => this.With(() => _statements.Add(
            SyntaxReferences.Invocation(target, method, arguments, _params, IsStatic, $"Method '{Name}'")));

    // A handle asserts the signature; validating here means a handle that exists is one
    // that matches, and freezing the parameters afterwards keeps it that way.
    private void ValidateHandle(params Type[] argumentTypes)
    {
        if (IsStatic)
            throw new InvalidOperationException(
                $"Method '{Name}' is static; static calls are not modelled yet. Emit the call with AddStatement.");

        if (_params.Count != argumentTypes.Length)
            throw new InvalidOperationException(
                $"Method '{Name}' declares {_params.Count} parameter(s) but the handle asserts {argumentTypes.Length}.");

        for (var i = 0; i < argumentTypes.Length; i++)
        {
            var asserted = TypeNameBuilder.New(argumentTypes[i]).ToString();
            var declared = _params[i].TypeName.ToString();

            if (!string.Equals(asserted, declared, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Method '{Name}' parameter {i + 1} ('{_params[i].Name}') is '{declared}', " +
                    $"but the handle asserts '{asserted}'.");
        }

        _handleIssued = true;
    }

    private void GuardParametersMutable()
    {
        if (_handleIssued)
            throw new InvalidOperationException(
                $"Method '{Name}' has issued a callable handle; parameters cannot change after that. " +
                "Declare all parameters first and take the handle last.");
    }

    /// <summary>
    /// Constrains a type parameter, e.g. <c>WithConstraint("T", "class")</c>,
    /// <c>WithConstraint("T", "IComparable&lt;T&gt;")</c>, or <c>WithConstraint("T", "new()")</c>.
    /// Call once per constraint; C# order is class/struct first, new() last.
    /// </summary>
    public MethodBuilder WithConstraint(string typeParameter, string constraint)
        => this.With(() => _generics.AddConstraint(typeParameter, constraint));

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("Obsolete")</c>.</summary>
    public MethodBuilder WithAttribute(string attribute) => this.With(() => _attributes.Add(SyntaxAttributes.AttributeList(attribute)));

    /// <summary>
    /// Gives the method an expression body: <c>Name(...) =&gt; expression;</c>. Valid for
    /// both void and value-returning methods.
    /// </summary>
    public MethodBuilder AsExpressionBody(string expression)
        => this.With(() => _expressionBody = SyntaxParse.Expression(expression));

    /// <summary>
    /// Appends a complete statement to the method body, e.g. <c>"return a + b;"</c>.
    /// A value-returning method's body must return on all paths.
    /// </summary>
    public MethodBuilder AddStatement(string statement)
        => this.With(() => _statements.Add(SyntaxBodies.Statement(statement)));

    /// <summary>Replaces the method body with the given statements.</summary>
    public MethodBuilder WithBody(params string[] statements)
        => this.With(() =>
        {
            _statements.Clear();
            foreach (var statement in statements ?? throw new ArgumentNullException(nameof(statements)))
                _statements.Add(SyntaxBodies.Statement(statement));
        });

    #endregion

    internal MethodDeclarationSyntax BuildMethod()
    {
        var method = BuildMethodCore();
        return _docs.IsEmpty ? method : method.WithLeadingTrivia(_docs.Build());
    }

    private MethodDeclarationSyntax BuildMethodCore()
    {
        ValidateInheritance();
        ValidateAsync();

        // AsCallable rejects a static method up front, but IsStatic can be set after the
        // handle exists; catching it here keeps the guard order-proof.
        if (_handleIssued && IsStatic)
            throw new InvalidOperationException(
                $"Method '{Name}' became static after issuing a callable handle; " +
                "calls through the handle would emit instance syntax.");

        var method = MethodDeclaration(_returnType, Identifier(Name))
            .WithAttributeLists(SyntaxAttributes.Lists(_attributes))
            .WithModifiers(SyntaxFormatting.Modifiers(
                AccessModifier, IsStatic, isPartial: IsPartial, inheritance: Inheritance, isAsync: IsAsync))
            .WithParameterList(SyntaxParameters.List(_params));

        method = _generics.ApplyTo(method, $"Method '{Name}'");

        // An abstract method declares no body at all — just a semicolon.
        if (IsAbstract)
            return method.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        if (_expressionBody is not null)
        {
            if (_statements.Count > 0)
                throw new InvalidOperationException(
                    $"Method '{Name}' cannot have both an expression body and statements.");

            return method
                .WithExpressionBody(ArrowExpressionClause(_expressionBody))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        // A statement block covers both void and value-returning methods; the caller is
        // responsible for returning on all paths when non-void.
        if (_statements.Count > 0)
            return method.WithBody(Block(_statements));

        // A non-void method with no body would emit `int Foo() { }`, which does not
        // compile: it needs either an expression body or statements.
        if (!_returnsVoid)
            throw new InvalidOperationException(
                $"Method '{Name}' returns non-void and needs a body. Use AsExpressionBody or AddStatement/WithBody.");

        return method.WithBody(Block());
    }

    // The inheritance modifiers are mutually exclusive by construction, so what remains
    // is their interaction with static, private, and the method body.
    private void ValidateInheritance()
    {
        if (Inheritance == Inheritance.None)
            return;

        if (IsStatic)
            throw new InvalidOperationException(
                $"Method '{Name}' cannot be both static and {Describe(Inheritance)}.");

        if (AccessModifier == AccessModifier.Private)
            throw new InvalidOperationException(
                $"Method '{Name}' cannot be private and {Describe(Inheritance)}.");

        if (IsPartial)
            throw new InvalidOperationException(
                $"Method '{Name}' cannot be both partial and {Describe(Inheritance)}.");

        if (IsAbstract && (_expressionBody is not null || _statements.Count > 0))
            throw new InvalidOperationException($"Abstract method '{Name}' cannot have a body.");
    }

    private void ValidateAsync()
    {
        if (!IsAsync)
            return;

        if (IsAbstract)
            throw new InvalidOperationException($"Method '{Name}' cannot be both abstract and async.");

        // Only the clearly-wrong cases are rejected: a built-in type other than void can
        // never be awaitable. Named types pass, so Task, ValueTask, IAsyncEnumerable and
        // any custom awaitable are all accepted without an allowlist to fall behind.
        if (_returnType is PredefinedTypeSyntax predefined
            && !predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            throw new InvalidOperationException(
                $"Async method '{Name}' cannot return '{predefined}'. Use void, Task, Task<T>, or another awaitable type.");
        }
    }

    private static string Describe(Inheritance inheritance)
        => inheritance == Inheritance.SealedOverride ? "sealed override" : inheritance.ToString().ToLowerInvariant();

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildMethod();

    internal override SyntaxNode BuildSyntax() => BuildMethod();
}
