using System;
using System.Collections.Generic;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// What every property accessor scope carries: which accessor it is, which property it
/// belongs to, and — the part that matters — whether that property is <c>static</c>.
/// </summary>
/// <remarks>
/// <para>
/// This base exists because of the shape of the bug it fixes. <c>IsStaticContext</c> is
/// <c>virtual … =&gt; false</c> on <see cref="StatementBuilder"/>, and every other
/// body-bearing member overrides it — methods, constructors, operators. The four
/// accessor scopes did not, so every static-context guard was dead inside a property
/// body and a <c>static</c> property emitted instance references (CS0120) and even
/// <c>this</c> (CS0026).
/// </para>
/// <para>
/// The durable fix is not four overrides: it is making staticness a <em>constructor
/// argument</em>, so a scope cannot be created without stating it and the next accessor
/// kind added — an event's <c>add</c>/<c>remove</c>, an indexer's — cannot inherit the
/// wrong default by omission.
/// </para>
/// </remarks>
/// <typeparam name="TSelf">The concrete scope type.</typeparam>
public abstract class AccessorBody<TSelf> : StatementBuilder<TSelf>
    where TSelf : AccessorBody<TSelf>
{
    private protected const string ValueParameterName = "value";

    private readonly string _accessor;
    private readonly string _propertyName;
    private readonly bool _isStatic;

    private protected AccessorBody(string accessor, string propertyName, bool isStatic)
        : base(accessor, _ => { })
    {
        _accessor = accessor;
        _propertyName = propertyName;
        _isStatic = isStatic;
    }

    private protected override bool IsStaticContext => _isStatic;

    private protected override string StatementContext
        => $"{(_accessor == "get" ? "Getter" : "Setter")} of '{_propertyName}'";

    internal List<StatementSyntax> BuiltStatements => Statements;

    internal override SyntaxNode BuildSyntax() => Block(Statements);
}

/// <summary>
/// The statement scope of a property getter, handed to the callback given to
/// <c>WithGetter</c>. Carries the whole shared statement API plus a
/// <see cref="Return"/> typed to the property.
/// </summary>
/// <typeparam name="T">The property's type.</typeparam>
public sealed class GetterBody<T> : AccessorBody<GetterBody<T>>
{
    internal GetterBody(string propertyName, bool isStatic) : base("get", propertyName, isStatic)
    {
    }

    /// <summary>
    /// Appends <c>return value;</c>. The value's type must be the property's, so
    /// returning the wrong member is a compile error in the generator.
    /// </summary>
    public GetterBody<T> Return(IValue<T> value)
    {
        AddReturn(value ?? throw new ArgumentNullException(nameof(value)));
        return this;
    }

    /// <summary>Appends <c>return literal;</c> for a constant of the property's type.</summary>
    public GetterBody<T> ReturnLiteral(T literal)
    {
        AddLiteralReturn(literal);
        return this;
    }
}

/// <summary>
/// The statement scope of a getter on a property whose type is named by text. Carries
/// the shared statement API plus an unchecked <see cref="Return"/>, since there is no
/// <c>T</c> for the returned value to be matched against.
/// </summary>
public sealed class RawGetterBody : AccessorBody<RawGetterBody>
{
    internal RawGetterBody(string propertyName, bool isStatic) : base("get", propertyName, isStatic)
    {
    }

    /// <summary>
    /// Appends <c>return value;</c>. Unchecked against the property's declared type,
    /// which is text rather than a <c>T</c> — but the value is still built rather than
    /// parsed, so it can be a reference, a raw member access, or a raw call.
    /// </summary>
    /// <param name="value">The value to return.</param>
    /// <returns>This scope, so the chain continues.</returns>
    public RawGetterBody Return(IValue value)
    {
        AddReturn(value ?? throw new ArgumentNullException(nameof(value)));
        return this;
    }
}

/// <summary>
/// The statement scope of a setter on a property whose type is named by text. The
/// incoming value is <see cref="Value"/>, which carries the property's declared type
/// text — so assigning it into a field declared with the same text is still checked by
/// <c>AssignRaw</c>.
/// </summary>
public sealed class RawSetterBody : AccessorBody<RawSetterBody>
{
    internal RawSetterBody(string propertyName, bool isStatic, string typeText)
        : base("set", propertyName, isStatic)
    {
        // As in the typed setter: `value` is genuinely in scope, so it belongs in the
        // parameter list, which is what makes a member of the same name qualify with
        // `this.` instead of silently binding the incoming value.
        Parameters.Add(Parameter.OfRawName(ValueParameterName, typeText, out var value));
        Value = value;
    }

    /// <summary>
    /// The value being assigned. Carries the property's declared type text, so
    /// <c>s.AssignRaw(backingField, s.Value)</c> still checks that the field agrees.
    /// </summary>
    public IRawReference Value { get; }
}

/// <summary>
/// The statement scope of a property setter, handed to the callback given to
/// <c>WithSetter</c>. The value being assigned is available as <see cref="Value"/>,
/// typed to the property.
/// </summary>
/// <typeparam name="T">The property's type.</typeparam>
public sealed class SetterBody<T> : AccessorBody<SetterBody<T>>
{
    internal SetterBody(string propertyName, bool isStatic) : base("set", propertyName, isStatic)
    {
        // `value` is genuinely in scope inside a setter, so it belongs in the parameter
        // list: that is what makes a member of the same name qualify with `this.`
        // instead of silently binding the incoming value.
        Parameters.Add(Parameter<T>.New(ValueParameterName));
        Value = new ParameterReference<T>(ValueParameterName);
    }

    /// <summary>
    /// The value being assigned, as a typed reference — so
    /// <c>s.Assign(backingField, s.Value)</c> checks that the field's type matches the
    /// property's.
    /// </summary>
    public IReference<T> Value { get; }
}
