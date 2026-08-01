using System.Collections.Generic;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

/// <summary>
/// Builds a field-like event declaration: <c>public event EventHandler Changed;</c>.
/// Obtained from <c>DefineEvent</c> on a type builder.
/// </summary>
public class EventBuilder : NamedBuilder, IAccessModifier, IMemberSyntaxBuilder
{
    private readonly TypeSyntax _handlerType;
    private readonly List<AttributeListSyntax> _attributes = [];
    private readonly DocComment _docs = new();

    internal EventBuilder(string name, TypeSyntax handlerType, AccessModifier accessModifier)
        : base(name, Identifiers.Validate)
    {
        _handlerType = handlerType;
        AccessModifier = accessModifier;
    }

    /// <summary>Whether the event is <c>static</c>.</summary>
    public bool IsStatic { get; set; }

    /// <summary>The event's accessibility.</summary>
    public AccessModifier AccessModifier { get; set; }

    #region FluentMethods

    /// <summary>Marks the event <c>static</c>.</summary>
    public EventBuilder Static() => this.With(() => IsStatic = true);

    /// <summary>Sets the event's accessibility.</summary>
    public EventBuilder WithAccessModifier(AccessModifier accessModifier)
        => this.With(() => AccessModifier = accessModifier);

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("field: NonSerialized")</c>.</summary>
    public EventBuilder WithAttribute(string attribute)
        => this.With(() => _attributes.Add(SyntaxAttributes.AttributeList(attribute)));

    /// <summary>Documents the event with an XML <c>&lt;summary&gt;</c>.</summary>
    public EventBuilder WithSummary(string text) => this.With(() => _docs.SetSummary(text));

    #endregion

    internal EventFieldDeclarationSyntax BuildEvent()
    {
        var @event = EventFieldDeclaration(VariableDeclaration(
                _handlerType,
                SingletonSeparatedList(VariableDeclarator(Identifier(Name)))))
            .WithAttributeLists(SyntaxAttributes.Lists(_attributes))
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier, IsStatic));

        return _docs.IsEmpty ? @event : @event.WithLeadingTrivia(_docs.Build());
    }

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildEvent();

    internal override SyntaxNode BuildSyntax() => BuildEvent();
}
