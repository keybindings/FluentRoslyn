using System;
using System.Collections.Generic;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// The statement scope of a property getter, handed to the callback given to
/// <c>WithGetter</c>. Carries the whole shared statement API plus a
/// <see cref="Return"/> typed to the property.
/// </summary>
/// <typeparam name="T">The property's type.</typeparam>
public sealed class GetterBody<T> : StatementBuilder<GetterBody<T>>
{
    private readonly string _propertyName;

    internal GetterBody(string propertyName) : base("get", _ => { })
    {
        _propertyName = propertyName;
    }

    private protected override string StatementContext => $"Getter of '{_propertyName}'";

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

    internal List<StatementSyntax> BuiltStatements => Statements;

    internal override SyntaxNode BuildSyntax() => Block(Statements);
}

/// <summary>
/// The statement scope of a getter on a property whose type is named by text. Carries
/// the shared statement API plus an unchecked <see cref="Return"/>, since there is no
/// <c>T</c> for the returned value to be matched against.
/// </summary>
public sealed class RawGetterBody : StatementBuilder<RawGetterBody>
{
    private readonly string _propertyName;

    internal RawGetterBody(string propertyName) : base("get", _ => { })
    {
        _propertyName = propertyName;
    }

    private protected override string StatementContext => $"Getter of '{_propertyName}'";

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

    internal List<StatementSyntax> BuiltStatements => Statements;

    internal override SyntaxNode BuildSyntax() => Block(Statements);
}

/// <summary>
/// The statement scope of a setter on a property whose type is named by text. The
/// incoming value is <see cref="Value"/>, which carries the property's declared type
/// text — so assigning it into a field declared with the same text is still checked by
/// <c>AssignRaw</c>.
/// </summary>
public sealed class RawSetterBody : StatementBuilder<RawSetterBody>
{
    private const string ValueParameterName = "value";

    private readonly string _propertyName;

    internal RawSetterBody(string propertyName, string typeText) : base("set", _ => { })
    {
        _propertyName = propertyName;

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

    private protected override string StatementContext => $"Setter of '{_propertyName}'";

    internal List<StatementSyntax> BuiltStatements => Statements;

    internal override SyntaxNode BuildSyntax() => Block(Statements);
}

/// <summary>
/// The statement scope of a property setter, handed to the callback given to
/// <c>WithSetter</c>. The value being assigned is available as <see cref="Value"/>,
/// typed to the property.
/// </summary>
/// <typeparam name="T">The property's type.</typeparam>
public sealed class SetterBody<T> : StatementBuilder<SetterBody<T>>
{
    private const string ValueParameterName = "value";

    private readonly string _propertyName;

    internal SetterBody(string propertyName) : base("set", _ => { })
    {
        _propertyName = propertyName;

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

    private protected override string StatementContext => $"Setter of '{_propertyName}'";

    internal List<StatementSyntax> BuiltStatements => Statements;

    internal override SyntaxNode BuildSyntax() => Block(Statements);
}
