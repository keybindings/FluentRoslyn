using System;
using System.Collections.Generic;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// What a type builder needs from a method regardless of its return type, so methods of
/// differing return types can share one member list.
/// </summary>
internal interface IMethodMember : INamedBuilder, IAccessModifier, IMemberSyntaxBuilder
{
    bool IsAbstract { get; }

    TypeDeclarationBuilder? DeclaringType { get; set; }
}

/// <summary>
/// Everything common to method builders: modifiers, generics, docs, bodies, callable
/// handles, and emission. <typeparamref name="TSelf"/> is the concrete kind, so fluent
/// methods return the void or value-returning builder as appropriate.
/// </summary>
/// <typeparam name="TSelf">The concrete method builder type.</typeparam>
public abstract class MethodBuilderBase<TSelf> : StatementBuilder<TSelf>, IMethodMember
    where TSelf : MethodBuilderBase<TSelf>
{
    private readonly List<AttributeListSyntax> _attributes = [];
    private readonly GenericParameters _generics = new();
    private readonly DocComment _docs = new();
    private ExpressionSyntax? _expressionBody;
    private bool _handleIssued;

    private protected TypeSyntax ReturnType;
    private protected bool ReturnsVoid;

    private protected MethodBuilderBase(
        string name,
        AccessModifier accessModifier,
        TypeSyntax returnType,
        bool returnsVoid) : base(name, Identifiers.Validate)
    {
        AccessModifier = accessModifier;
        ReturnType = returnType;
        ReturnsVoid = returnsVoid;
    }

    /// <summary>Whether the method is <c>static</c>.</summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// The type this method is declared on, set when the type builder takes it. Null
    /// only for a method that was never attached to a type.
    /// </summary>
    internal TypeDeclarationBuilder? DeclaringType { get; set; }

    TypeDeclarationBuilder? IMethodMember.DeclaringType
    {
        get => DeclaringType;
        set => DeclaringType = value;
    }

    /// <summary>Whether the method is <c>partial</c>.</summary>
    public bool IsPartial { get; set; }

    /// <summary>
    /// Whether the method is <c>async</c>. The return type must be awaitable —
    /// <c>void</c>, <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>, and so on.
    /// </summary>
    public bool IsAsync { get; set; }

    /// <summary>The inheritance modifier — virtual, abstract, override, or sealed override.</summary>
    public Inheritance Inheritance { get; set; }

    /// <summary>Whether the method is <c>abstract</c>, which means it declares no body.</summary>
    public bool IsAbstract => Inheritance == Inheritance.Abstract;

    /// <summary>The method's accessibility.</summary>
    public AccessModifier AccessModifier { get; set; }

    private protected override string StatementContext => $"Method '{Name}'";

    private protected override bool IsStaticContext => IsStatic;

    private protected override void OnParametersMutating()
    {
        if (_handleIssued)
            throw new InvalidOperationException(
                $"Method '{Name}' has issued a callable handle; parameters cannot change after that. " +
                "Declare all parameters first and take the handle last.");
    }

    #region FluentMethods

    /// <summary>Marks the method <c>static</c>.</summary>
    public TSelf Static()
    {
        IsStatic = true;
        return Self;
    }

    /// <summary>Marks the method <c>partial</c> (e.g. a source generator implementing a partial method).</summary>
    public TSelf Partial()
    {
        IsPartial = true;
        return Self;
    }

    /// <summary>
    /// Marks the method <c>async</c>. Pair it with an awaitable return type, e.g.
    /// <c>DefineMethod&lt;Task&gt;("SaveAsync").Async()</c>.
    /// </summary>
    public TSelf Async()
    {
        IsAsync = true;
        return Self;
    }

    /// <summary>Marks the method <c>virtual</c>.</summary>
    public TSelf Virtual()
    {
        Inheritance = Inheritance.Virtual;
        return Self;
    }

    /// <summary>
    /// Marks the method <c>abstract</c>: it emits no body, and the declaring type must
    /// itself be abstract.
    /// </summary>
    public TSelf Abstract()
    {
        Inheritance = Inheritance.Abstract;
        return Self;
    }

    /// <summary>Marks the method <c>override</c>.</summary>
    public TSelf Override()
    {
        Inheritance = Inheritance.Override;
        return Self;
    }

    /// <summary>Marks the method <c>sealed override</c>.</summary>
    public TSelf SealedOverride()
    {
        Inheritance = Inheritance.SealedOverride;
        return Self;
    }

    /// <summary>Sets the method's accessibility.</summary>
    public TSelf WithAccessModifier(AccessModifier accessModifier)
    {
        AccessModifier = accessModifier;
        return Self;
    }

    /// <summary>
    /// Documents the method with an XML <c>&lt;summary&gt;</c>. Newlines become separate
    /// comment lines, and XML markup characters are escaped.
    /// </summary>
    public TSelf WithSummary(string text)
    {
        _docs.SetSummary(text);
        return Self;
    }

    /// <summary>
    /// Documents a parameter: <c>&lt;param name="..."&gt;</c>. The name must match a
    /// parameter added with <see cref="StatementBuilder{TSelf}.WithParameter{T}(string)"/>.
    /// </summary>
    public TSelf WithParameterDoc(string parameterName, string text)
    {
        _docs.AddParameter(parameterName, text);
        return Self;
    }

    /// <summary>Documents the return value: <c>&lt;returns&gt;</c>.</summary>
    public TSelf WithReturnsDoc(string text)
    {
        _docs.SetReturns(text);
        return Self;
    }

    /// <summary>Adds a generic type parameter, e.g. <c>WithTypeParameter("T")</c> for <c>Name&lt;T&gt;</c>.</summary>
    public TSelf WithTypeParameter(string name)
    {
        _generics.AddTypeParameter(name);
        return Self;
    }

    /// <summary>
    /// Constrains a type parameter, e.g. <c>WithConstraint("T", "class")</c>,
    /// <c>WithConstraint("T", "IComparable&lt;T&gt;")</c>, or <c>WithConstraint("T", "new()")</c>.
    /// Call once per constraint; C# order is class/struct first, new() last.
    /// </summary>
    public TSelf WithConstraint(string typeParameter, string constraint)
    {
        _generics.AddConstraint(typeParameter, constraint);
        return Self;
    }

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("Obsolete")</c>.</summary>
    public TSelf WithAttribute(string attribute)
    {
        _attributes.Add(SyntaxAttributes.AttributeList(attribute));
        return Self;
    }

    /// <summary>
    /// Gives the method an expression body: <c>Name(...) =&gt; expression;</c>. Valid for
    /// both void and value-returning methods.
    /// </summary>
    public TSelf AsExpressionBody(string expression)
    {
        _expressionBody = SyntaxParse.Expression(expression);
        return Self;
    }

    /// <summary>
    /// Hands back a typed handle to this parameterless method, for emitting
    /// type-checked calls with <c>Call</c>. Take the handle after the parameters are
    /// declared — the signature freezes once a handle exists.
    /// </summary>
    public TSelf AsCallable(out IMethod method)
    {
        ValidateHandle();
        method = new MethodHandle0(Name);
        return Self;
    }

    /// <summary>
    /// Hands back a typed handle to this one-parameter method. The type argument is
    /// validated against the declared parameter, so a handle that exists is a handle
    /// that matches — and a call through it type-checks in the generator.
    /// </summary>
    public TSelf AsCallable<T1>(out IMethod<T1> method)
    {
        ValidateHandle(typeof(T1));
        method = new MethodHandle1<T1>(Name);
        return Self;
    }

    /// <summary>Hands back a typed handle to this two-parameter method.</summary>
    public TSelf AsCallable<T1, T2>(out IMethod<T1, T2> method)
    {
        ValidateHandle(typeof(T1), typeof(T2));
        method = new MethodHandle2<T1, T2>(Name);
        return Self;
    }

    /// <summary>Hands back a typed handle to this three-parameter method.</summary>
    public TSelf AsCallable<T1, T2, T3>(out IMethod<T1, T2, T3> method)
    {
        ValidateHandle(typeof(T1), typeof(T2), typeof(T3));
        method = new MethodHandle3<T1, T2, T3>(Name);
        return Self;
    }

    /// <summary>
    /// Hands back a handle to this parameterless method that also carries its declaring
    /// type, so a call through it checks the receiver. <typeparamref name="TDeclaring"/>
    /// must name the declaring type — its <c>[EmitsAs]</c> placeholder when that type is
    /// being generated.
    /// </summary>
    public TSelf AsCallableOn<TDeclaring>(out IMethodOn<TDeclaring> method)
    {
        ValidateReceiver(typeof(TDeclaring));
        ValidateHandle();
        method = new MethodHandleOn0<TDeclaring>(Name);
        return Self;
    }

    /// <summary>
    /// Hands back a receiver-typed handle to this one-parameter method. A call through
    /// it checks both the receiver and the argument.
    /// </summary>
    public TSelf AsCallableOn<TDeclaring, T1>(out IMethodOn<TDeclaring, T1> method)
    {
        ValidateReceiver(typeof(TDeclaring));
        ValidateHandle(typeof(T1));
        method = new MethodHandleOn1<TDeclaring, T1>(Name);
        return Self;
    }

    /// <summary>Hands back a receiver-typed handle to this two-parameter method.</summary>
    public TSelf AsCallableOn<TDeclaring, T1, T2>(out IMethodOn<TDeclaring, T1, T2> method)
    {
        ValidateReceiver(typeof(TDeclaring));
        ValidateHandle(typeof(T1), typeof(T2));
        method = new MethodHandleOn2<TDeclaring, T1, T2>(Name);
        return Self;
    }

    /// <summary>Hands back a receiver-typed handle to this three-parameter method.</summary>
    public TSelf AsCallableOn<TDeclaring, T1, T2, T3>(out IMethodOn<TDeclaring, T1, T2, T3> method)
    {
        ValidateReceiver(typeof(TDeclaring));
        ValidateHandle(typeof(T1), typeof(T2), typeof(T3));
        method = new MethodHandleOn3<TDeclaring, T1, T2, T3>(Name);
        return Self;
    }

    #endregion

    // A handle asserts the signature; validating here means a handle that exists is one
    // that matches, and freezing the parameters afterwards keeps it that way.
    private protected void ValidateHandle(params Type[] argumentTypes)
    {
        if (IsStatic)
            throw new InvalidOperationException(
                $"Method '{Name}' is static; static calls are not modelled yet. Emit the call with AddStatement.");

        if (Parameters.Count != argumentTypes.Length)
            throw new InvalidOperationException(
                $"Method '{Name}' declares {Parameters.Count} parameter(s) but the handle asserts {argumentTypes.Length}.");

        for (var i = 0; i < argumentTypes.Length; i++)
        {
            var asserted = TypeNameBuilder.New(argumentTypes[i]).ToString();
            var declared = Parameters[i].TypeName.ToString();

            if (!string.Equals(asserted, declared, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Method '{Name}' parameter {i + 1} ('{Parameters[i].Name}') is '{declared}', " +
                    $"but the handle asserts '{asserted}'.");
        }

        _handleIssued = true;
    }

    // The pairing that makes receiver checking work: the placeholder's emitted name and
    // the declaring type's qualified name have to be the same string, since that is what
    // both will be in the generated source.
    private protected void ValidateReceiver(Type declaringType)
    {
        if (DeclaringType is null)
            throw new InvalidOperationException(
                $"Method '{Name}' is not attached to a type, so it has no receiver to check. " +
                "Define it with DefineMethod on a type builder.");

        var asserted = TypeNameBuilder.New(declaringType).ToString();
        var declared = DeclaringType.BuildTypeSyntax().ToString();

        if (!string.Equals(asserted, declared, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Method '{Name}' is declared on '{declared}', but the handle asserts '{asserted}'. " +
                "The type argument must name the declaring type — its [EmitsAs] placeholder when " +
                "that type is being generated.");
    }

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

        var method = MethodDeclaration(ReturnType, Identifier(Name))
            .WithAttributeLists(SyntaxAttributes.Lists(_attributes))
            .WithModifiers(SyntaxFormatting.Modifiers(
                AccessModifier, IsStatic, isPartial: IsPartial, inheritance: Inheritance, isAsync: IsAsync))
            .WithParameterList(SyntaxParameters.List(Parameters));

        method = _generics.ApplyTo(method, $"Method '{Name}'");

        // An abstract method declares no body at all — just a semicolon.
        if (IsAbstract)
            return method.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        if (_expressionBody is not null)
        {
            if (Statements.Count > 0)
                throw new InvalidOperationException(
                    $"Method '{Name}' cannot have both an expression body and statements.");

            return method
                .WithExpressionBody(ArrowExpressionClause(_expressionBody))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        // A statement block covers both void and value-returning methods; the caller is
        // responsible for returning on all paths when non-void.
        if (Statements.Count > 0)
            return method.WithBody(Block(Statements));

        // A non-void method with no body would emit `int Foo() { }`, which does not
        // compile: it needs either an expression body or statements.
        if (!ReturnsVoid)
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

        if (IsAbstract && (_expressionBody is not null || Statements.Count > 0))
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
        if (ReturnType is PredefinedTypeSyntax predefined
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

/// <summary>
/// Builds a method with no declared return type of its own — <c>void</c>, or a raw type
/// name set with <see cref="Returns(string)"/>. Obtained from <c>DefineMethod</c> on a
/// type builder.
/// </summary>
public class MethodBuilder : MethodBuilderBase<MethodBuilder>
{
    private MethodBuilder(string name, AccessModifier accessModifier, TypeSyntax returnType, bool returnsVoid)
        : base(name, accessModifier, returnType, returnsVoid)
    {
    }

    /// <summary>A void method: <c>void Name(...) { }</c>.</summary>
    internal static MethodBuilder Action(string name, AccessModifier accessModifier)
        => new(name, accessModifier, PredefinedType(Token(SyntaxKind.VoidKeyword)), returnsVoid: true);

    /// <summary>
    /// Sets the return type from a raw type name, e.g. <c>Returns("T")</c> or
    /// <c>Returns("List&lt;T&gt;")</c> — for returning a generic type parameter that is
    /// not a CLR type. Requires a body. Since the type is a string, <c>Return</c> cannot
    /// be checked against it; use <c>AddStatement</c> for the return.
    /// </summary>
    public MethodBuilder Returns(string typeName)
    {
        ReturnType = SyntaxParse.TypeName(typeName);
        ReturnsVoid = false;
        return this;
    }

    /// <summary>
    /// Sets the return type to a type being generated alongside — the type's builder is
    /// the reference, so the name is spelled once. Requires a body.
    /// </summary>
    public MethodBuilder Returns(TypeDeclarationBuilder type)
    {
        ReturnType = TypeNameBuilder.For(type).BuildTypeSyntax();
        ReturnsVoid = false;
        return this;
    }

    /// <summary>
    /// Appends a bare <c>return;</c>. Only valid on a void method — a method with a
    /// return type must return a value.
    /// </summary>
    public MethodBuilder Return()
    {
        if (!ReturnsVoid)
            throw new InvalidOperationException(
                $"Method '{Name}' has a return type, so a bare 'return;' would not compile. " +
                "Use DefineMethod<T> and Return(value), or AddStatement for a raw return type.");

        AddReturn(null);
        return this;
    }
}

/// <summary>
/// Builds a method returning <typeparamref name="TReturn"/>. Obtained from
/// <c>DefineMethod&lt;TReturn&gt;</c> on a type builder. Carrying the return type as a
/// type argument is what lets <see cref="Return"/> be checked by the compiler.
/// </summary>
/// <typeparam name="TReturn">The method's return type.</typeparam>
public class MethodBuilder<TReturn> : MethodBuilderBase<MethodBuilder<TReturn>>
{
    private MethodBuilder(string name, AccessModifier accessModifier)
        : base(name, accessModifier, TypeNameBuilder.New<TReturn>().BuildTypeSyntax(), returnsVoid: false)
    {
    }

    internal static MethodBuilder<TReturn> Returning(string name, AccessModifier accessModifier)
        => new(name, accessModifier);

    /// <summary>
    /// Appends <c>return value;</c>. The value's type must be
    /// <typeparamref name="TReturn"/>, so returning the wrong thing is a compile error in
    /// the generator rather than generated source that does not build.
    /// </summary>
    public MethodBuilder<TReturn> Return(IValue<TReturn> value)
    {
        AddReturn(value ?? throw new ArgumentNullException(nameof(value)));
        return this;
    }

    /// <summary>
    /// Appends <c>return literal;</c> for a constant of the method's return type, e.g.
    /// <c>DefineMethod&lt;bool&gt;("IsValid").ReturnLiteral(true)</c>. Named apart from
    /// <see cref="Return(IValue{TReturn})"/> because a value that is itself a
    /// reference type would make the two overloads ambiguous.
    /// </summary>
    public MethodBuilder<TReturn> ReturnLiteral(TReturn literal)
    {
        AddLiteralReturn(literal);
        return this;
    }

    #region FunctionHandles

    /// <summary>
    /// Hands back a handle to this parameterless method whose <em>result</em> is usable
    /// as a value, so <c>target.Invoke(handle)</c> can be assigned or returned.
    /// </summary>
    /// <remarks>
    /// Named apart from <c>AsCallable</c>, and living only on the value-returning builder,
    /// because <see cref="IMethod"/> asserts argument types and cannot carry a result.
    /// <typeparamref name="TReturn"/> is supplied by this builder rather than asserted by
    /// the caller, so it cannot disagree with the declared return type.
    /// </remarks>
    /// <param name="function">Receives the handle.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public MethodBuilder<TReturn> AsFunction(out IFunction<TReturn> function)
    {
        ValidateHandle();
        function = new FunctionHandle0<TReturn>(Name);
        return this;
    }

    /// <summary>Hands back a value-producing handle to this one-parameter method.</summary>
    /// <typeparam name="T1">The parameter's type, validated against the declared one.</typeparam>
    /// <param name="function">Receives the handle.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public MethodBuilder<TReturn> AsFunction<T1>(out IFunction<TReturn, T1> function)
    {
        ValidateHandle(typeof(T1));
        function = new FunctionHandle1<TReturn, T1>(Name);
        return this;
    }

    /// <summary>Hands back a value-producing handle to this two-parameter method.</summary>
    /// <typeparam name="T1">The first parameter's type.</typeparam>
    /// <typeparam name="T2">The second parameter's type.</typeparam>
    /// <param name="function">Receives the handle.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public MethodBuilder<TReturn> AsFunction<T1, T2>(out IFunction<TReturn, T1, T2> function)
    {
        ValidateHandle(typeof(T1), typeof(T2));
        function = new FunctionHandle2<TReturn, T1, T2>(Name);
        return this;
    }

    /// <summary>Hands back a value-producing handle to this three-parameter method.</summary>
    /// <typeparam name="T1">The first parameter's type.</typeparam>
    /// <typeparam name="T2">The second parameter's type.</typeparam>
    /// <typeparam name="T3">The third parameter's type.</typeparam>
    /// <param name="function">Receives the handle.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public MethodBuilder<TReturn> AsFunction<T1, T2, T3>(out IFunction<TReturn, T1, T2, T3> function)
    {
        ValidateHandle(typeof(T1), typeof(T2), typeof(T3));
        function = new FunctionHandle3<TReturn, T1, T2, T3>(Name);
        return this;
    }

    /// <summary>
    /// Hands back a value-producing handle that also carries the declaring type, so
    /// <c>InvokeOn</c> checks the receiver as well as the arguments and the result.
    /// </summary>
    /// <typeparam name="TDeclaring">The declaring type — its <c>[EmitsAs]</c> placeholder when it is being generated.</typeparam>
    /// <param name="function">Receives the handle.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public MethodBuilder<TReturn> AsFunctionOn<TDeclaring>(out IFunctionOn<TDeclaring, TReturn> function)
    {
        ValidateReceiver(typeof(TDeclaring));
        ValidateHandle();
        function = new FunctionHandleOn0<TDeclaring, TReturn>(Name);
        return this;
    }

    /// <summary>Hands back a receiver-typed, value-producing handle to this one-parameter method.</summary>
    /// <typeparam name="TDeclaring">The declaring type.</typeparam>
    /// <typeparam name="T1">The parameter's type.</typeparam>
    /// <param name="function">Receives the handle.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public MethodBuilder<TReturn> AsFunctionOn<TDeclaring, T1>(out IFunctionOn<TDeclaring, TReturn, T1> function)
    {
        ValidateReceiver(typeof(TDeclaring));
        ValidateHandle(typeof(T1));
        function = new FunctionHandleOn1<TDeclaring, TReturn, T1>(Name);
        return this;
    }

    /// <summary>Hands back a receiver-typed, value-producing handle to this two-parameter method.</summary>
    /// <typeparam name="TDeclaring">The declaring type.</typeparam>
    /// <typeparam name="T1">The first parameter's type.</typeparam>
    /// <typeparam name="T2">The second parameter's type.</typeparam>
    /// <param name="function">Receives the handle.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public MethodBuilder<TReturn> AsFunctionOn<TDeclaring, T1, T2>(
        out IFunctionOn<TDeclaring, TReturn, T1, T2> function)
    {
        ValidateReceiver(typeof(TDeclaring));
        ValidateHandle(typeof(T1), typeof(T2));
        function = new FunctionHandleOn2<TDeclaring, TReturn, T1, T2>(Name);
        return this;
    }

    /// <summary>Hands back a receiver-typed, value-producing handle to this three-parameter method.</summary>
    /// <typeparam name="TDeclaring">The declaring type.</typeparam>
    /// <typeparam name="T1">The first parameter's type.</typeparam>
    /// <typeparam name="T2">The second parameter's type.</typeparam>
    /// <typeparam name="T3">The third parameter's type.</typeparam>
    /// <param name="function">Receives the handle.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public MethodBuilder<TReturn> AsFunctionOn<TDeclaring, T1, T2, T3>(
        out IFunctionOn<TDeclaring, TReturn, T1, T2, T3> function)
    {
        ValidateReceiver(typeof(TDeclaring));
        ValidateHandle(typeof(T1), typeof(T2), typeof(T3));
        function = new FunctionHandleOn3<TDeclaring, TReturn, T1, T2, T3>(Name);
        return this;
    }

    #endregion
}
