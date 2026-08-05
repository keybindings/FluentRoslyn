using System;
using System.Collections.Generic;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FluentRoslyn.Builders;

/// <summary>
/// Everything a body-bearing member needs to hold statements: the parameter list the
/// statements can refer to, the statements themselves, and the emission logic. Carries
/// no fluent methods — those live in <see cref="StatementBuilder{TSelf}"/>, which knows
/// what to return.
/// </summary>
/// <remarks>
/// Split non-generic so member lists can be typed without naming a self type, and so
/// the validation and shadowing rules exist exactly once. Before this, the whole
/// statement API was duplicated between the method and constructor builders.
/// </remarks>
public abstract class StatementBuilder : NamedBuilder
{
    private protected StatementBuilder(string name, Action<string> validNameCheck) : base(name, validNameCheck)
    {
    }

    private protected readonly List<IParameter> Parameters = [];
    private protected readonly List<StatementSyntax> Statements = [];

    /// <summary>How this member names itself in error messages, e.g. <c>Method 'Foo'</c>.</summary>
    private protected abstract string StatementContext { get; }

    /// <summary>
    /// Whether <c>this.</c> is unavailable, which decides what happens when a parameter
    /// shadows a member being referenced.
    /// </summary>
    private protected virtual bool IsStaticContext => false;

    /// <summary>
    /// Called before the parameter list changes, for builders that freeze their
    /// signature once something depends on it.
    /// </summary>
    private protected virtual void OnParametersMutating()
    {
    }

    private protected void AddParameter(IParameter parameter)
    {
        OnParametersMutating();
        Parameters.Add(parameter);
    }

    private protected void AddAssignment<TValue>(IReference<TValue> target, IValue<TValue> value)
        => Statements.Add(SyntaxReferences.Assignment(target, value, Parameters, IsStaticContext, StatementContext));

    private protected void AddInvocation(IReference target, object method, IValue[] arguments)
        => Statements.Add(SyntaxReferences.Invocation(
            target, method, arguments, Parameters, IsStaticContext, StatementContext));

    private protected void AddRawStatement(string statement)
        => Statements.Add(SyntaxBodies.Statement(statement));

    private protected void AddReturn(IValue? value)
        => Statements.Add(SyntaxReferences.Return(value, Parameters, IsStaticContext, StatementContext));

    private protected void AddLiteralAssignment(IReference target, object? literal)
        => Statements.Add(SyntaxReferences.AssignmentOfLiteral(
            target, literal, Parameters, IsStaticContext, StatementContext));

    private protected void AddLiteralReturn(object? literal)
        => Statements.Add(SyntaxReferences.ReturnLiteral(literal));

    private protected void AddNullGuard(IReference value)
        => Statements.Add(SyntaxReferences.ThrowIfNull(value, Parameters, IsStaticContext, StatementContext));

    private protected void AddCompoundAssignment<TValue>(
        IReference<TValue> target, SyntaxKind kind, IValue<TValue> value)
        => Statements.Add(SyntaxReferences.CompoundAssignment(
            target, kind, value, Parameters, IsStaticContext, StatementContext));

    private protected void AddCompoundLiteralAssignment(IReference target, SyntaxKind kind, object? literal)
        => Statements.Add(SyntaxReferences.CompoundAssignmentOfLiteral(
            target, kind, literal, Parameters, IsStaticContext, StatementContext));

    private protected void ReplaceStatements(string[] statements)
    {
        Statements.Clear();
        foreach (var statement in statements ?? throw new ArgumentNullException(nameof(statements)))
            Statements.Add(SyntaxBodies.Statement(statement));
    }
}

/// <summary>
/// The fluent statement API, returning the concrete builder so chaining survives.
/// TSelf is the concrete kind, following the same CRTP shape as
/// <see cref="TypeBuilder{TSelf}"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete builder type.</typeparam>
public abstract class StatementBuilder<TSelf> : StatementBuilder
    where TSelf : StatementBuilder<TSelf>
{
    private protected StatementBuilder(string name, Action<string> validNameCheck) : base(name, validNameCheck)
    {
    }

    /// <summary>This builder as its concrete type, for fluent returns.</summary>
    private protected TSelf Self => (TSelf)this;

    /// <summary>Appends a parameter of type <typeparamref name="T"/>.</summary>
    public TSelf WithParameter<T>(string name)
    {
        AddParameter(Parameter<T>.New(name));
        return Self;
    }

    /// <summary>
    /// Appends a parameter of type <typeparamref name="T"/> and hands back a typed
    /// reference to it. Returns the builder, so the fluent chain is unbroken:
    /// <c>.WithParameter&lt;int&gt;("id", out var id)</c>.
    /// </summary>
    public TSelf WithParameter<T>(string name, out IReference<T> reference)
    {
        var parameter = Parameter<T>.New(name);
        AddParameter(parameter);
        reference = new ParameterReference<T>(parameter.Name);
        return Self;
    }

    /// <summary>
    /// Appends a parameter whose type is being generated alongside — the type's builder
    /// is the reference, so the name is spelled once. For a typed handle to the
    /// parameter, give the generated type an <c>[EmitsAs]</c> placeholder and use
    /// <see cref="WithParameter{T}(string, out IReference{T})"/> instead.
    /// </summary>
    public TSelf WithParameter(TypeDeclarationBuilder type, string name)
    {
        AddParameter(Parameter.Of(type, name));
        return Self;
    }

    /// <summary>
    /// Appends a parameter whose type is named by text — for a type the generator cannot
    /// name as a type argument, above all one discovered from the consumer's compilation
    /// as an <c>ISymbol</c>.
    /// </summary>
    /// <remarks>
    /// No <c>out IReference&lt;T&gt;</c> companion, deliberately: there is no
    /// <c>T</c> for a reference to carry, so a body using this parameter goes through
    /// <c>AddStatement</c>. Transposing the arguments is caught, since the name is
    /// validated as a C# identifier and a qualified type name is not one.
    /// </remarks>
    /// <param name="name">The parameter's name.</param>
    /// <param name="typeName">The parameter's type, as C# text. Parsed, so a malformed name is rejected.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public TSelf WithParameter(string name, string typeName)
    {
        AddParameter(Parameter.OfRawName(name, typeName));
        return Self;
    }

    /// <summary>
    /// Appends an assignment statement, e.g. <c>Name = name;</c>. The value's type must
    /// be the target's, so assigning the wrong one is a compile error in the generator
    /// rather than broken generated source. The target names a location, so it stays a
    /// reference; the value may be anything that produces one.
    /// </summary>
    public TSelf Assign<TValue>(IReference<TValue> target, IValue<TValue> value)
    {
        AddAssignment(target, value);
        return Self;
    }

    /// <summary>
    /// Appends an assignment from a constant, e.g. <c>Count = 0;</c> or
    /// <c>Name = null;</c>. The literal's type must match the target's, so
    /// <c>AssignLiteral(intProperty, "text")</c> is a compile error. Covers the
    /// primitives with a natural C# literal form; anything else needs a raw statement.
    /// </summary>
    /// <remarks>
    /// Named apart from <see cref="Assign{TValue}(IReference{TValue}, IValue{TValue})"/>
    /// rather than overloading it: for a
    /// reference type, a value convertible to both <c>IReference&lt;T&gt;</c> and
    /// <c>T</c> — <c>null</c> most obviously — makes the two indistinguishable, and the
    /// call site fails with an ambiguity error that explains nothing about the intent.
    /// </remarks>
    public TSelf AssignLiteral<TValue>(IReference<TValue> target, TValue literal)
    {
        AddLiteralAssignment(target, literal);
        return Self;
    }

    /// <summary>
    /// Appends a compound assignment, e.g. <c>Count += delta;</c>. Typed like simple
    /// assignment: a reference target, a value of its type.
    /// </summary>
    public TSelf Assign<TValue>(IReference<TValue> target, AssignmentOperator op, IValue<TValue> value)
    {
        AddCompoundAssignment(target, SyntaxReferences.KindOf(op), value);
        return Self;
    }

    /// <summary>
    /// Appends a compound assignment from a constant, e.g. <c>Count += 1;</c>.
    /// </summary>
    public TSelf AssignLiteral<TValue>(IReference<TValue> target, AssignmentOperator op, TValue literal)
    {
        AddCompoundLiteralAssignment(target, SyntaxReferences.KindOf(op), literal);
        return Self;
    }

    /// <summary>
    /// Appends a null-coalescing assignment: <c>target ??= value;</c>. Separate from the
    /// other operators because it needs a target that can be null, which the shared
    /// signature cannot state.
    /// </summary>
    public TSelf AssignIfNull<TValue>(IReference<TValue> target, IValue<TValue> value) where TValue : class
    {
        AddCompoundAssignment(target, SyntaxKind.CoalesceAssignmentExpression, value);
        return Self;
    }

    /// <summary>
    /// Appends a null-coalescing assignment from a constant, e.g.
    /// <c>Name ??= "unnamed";</c>.
    /// </summary>
    public TSelf AssignIfNullLiteral<TValue>(IReference<TValue> target, TValue literal) where TValue : class
    {
        AddCompoundLiteralAssignment(target, SyntaxKind.CoalesceAssignmentExpression, literal);
        return Self;
    }

    /// <summary>
    /// Appends a null guard: <c>if (x is null) throw new ArgumentNullException(nameof(x));</c>.
    /// Constrained to reference types, since a null check on a non-nullable value type
    /// is meaningless.
    /// </summary>
    public TSelf ThrowIfNull<TValue>(IReference<TValue> value) where TValue : class
    {
        AddNullGuard(value);
        return Self;
    }

    /// <summary>Appends a call statement: <c>target.Method();</c>.</summary>
    public TSelf Call<TTarget>(IReference<TTarget> target, IMethod method)
        => AddCall(target, method);

    /// <summary>
    /// Appends a call statement: <c>target.Method(argument1);</c>. The argument
    /// reference's type must match the handle's — a mismatch is a compile error in the
    /// generator rather than broken generated source.
    /// </summary>
    public TSelf Call<TTarget, T1>(IReference<TTarget> target, IMethod<T1> method, IValue<T1> argument1)
        => AddCall(target, method, argument1);

    /// <summary>Appends a two-argument call statement.</summary>
    public TSelf Call<TTarget, T1, T2>(
        IReference<TTarget> target, IMethod<T1, T2> method, IValue<T1> argument1, IValue<T2> argument2)
        => AddCall(target, method, argument1, argument2);

    /// <summary>Appends a three-argument call statement.</summary>
    public TSelf Call<TTarget, T1, T2, T3>(
        IReference<TTarget> target, IMethod<T1, T2, T3> method,
        IValue<T1> argument1, IValue<T2> argument2, IValue<T3> argument3)
        => AddCall(target, method, argument1, argument2, argument3);

    /// <summary>
    /// Appends a call statement whose receiver is checked: the target must be a
    /// reference to <typeparamref name="TDeclaring"/>, the type declaring the method.
    /// Named apart from <see cref="Call{TTarget}"/> so a receiver/handle disagreement
    /// reports as a failed inference on this method rather than as a conversion error
    /// against the untyped overload, which is the surviving candidate but not the cause.
    /// </summary>
    public TSelf CallOn<TDeclaring>(IReference<TDeclaring> target, IMethodOn<TDeclaring> method)
        => AddCall(target, method);

    /// <summary>Appends a receiver-checked call with one argument.</summary>
    public TSelf CallOn<TDeclaring, T1>(
        IReference<TDeclaring> target, IMethodOn<TDeclaring, T1> method, IValue<T1> argument1)
        => AddCall(target, method, argument1);

    /// <summary>Appends a receiver-checked call with two arguments.</summary>
    public TSelf CallOn<TDeclaring, T1, T2>(
        IReference<TDeclaring> target, IMethodOn<TDeclaring, T1, T2> method,
        IValue<T1> argument1, IValue<T2> argument2)
        => AddCall(target, method, argument1, argument2);

    /// <summary>Appends a receiver-checked call with three arguments.</summary>
    public TSelf CallOn<TDeclaring, T1, T2, T3>(
        IReference<TDeclaring> target, IMethodOn<TDeclaring, T1, T2, T3> method,
        IValue<T1> argument1, IValue<T2> argument2, IValue<T3> argument3)
        => AddCall(target, method, argument1, argument2, argument3);

    /// <summary>Appends a complete statement from raw C# text.</summary>
    public TSelf AddStatement(string statement)
    {
        AddRawStatement(statement);
        return Self;
    }

    /// <summary>Replaces the body with the given raw statements.</summary>
    public TSelf WithBody(params string[] statements)
    {
        ReplaceStatements(statements);
        return Self;
    }

    private TSelf AddCall(IReference target, object method, params IValue[] arguments)
    {
        AddInvocation(target, method, arguments);
        return Self;
    }
}
