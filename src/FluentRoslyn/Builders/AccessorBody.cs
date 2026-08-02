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
    /// Appends <c>return value;</c>. The reference's type must be the property's, so
    /// returning the wrong member is a compile error in the generator.
    /// </summary>
    public GetterBody<T> Return(IReference<T> value)
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
