using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

public class ClassBuilder : NamedBuilder
{
    private readonly List<FieldBuilder> _fields = [];
    private readonly List<PropertyBuilder> _properties = [];
    private readonly List<MethodBuilder> _methods = [];

    internal ClassBuilder(NamespaceBuilder @namespace, string name) : base(name, NameValidation)
    {
        Namespace = @namespace;
    }

    public bool IsFileScopedNamespace { get; set; } = true;

    public bool IsStatic { get; set; }

    public bool IsPartial { get; set; }

    public NamespaceBuilder Namespace { get; }

    public AccessModifier AccessModifier { get; set; } = AccessModifier.Public;

    // TODO: emit a base-type list once inheritance/interface composition is designed. Stored only for now.
    public ClassBuilder? ParentType { get; set; }

    #region FluentMethods

    public ClassBuilder Static() => With(() => IsStatic = true);

    public ClassBuilder Partial() => With(() => IsPartial = true);

    public ClassBuilder WithAccessModifier(AccessModifier accessModifier) => With(() => AccessModifier = accessModifier);

    public ClassBuilder BlockScopedNamespace() => With(() => IsFileScopedNamespace = false);

    public ClassBuilder WithParent(ClassBuilder type) => With(() => ParentType = type);

    #endregion

    #region Members

    public FieldBuilder<T> DefineField<T>(string name)
        => DefineField<T>(name, AccessModifier.Private);

    public FieldBuilder<T> DefineField<T>(string name, AccessModifier accessModifier)
    {
        var fb = new FieldBuilder<T>(this, name, accessModifier);
        _fields.Add(fb);
        return fb;
    }

    public PropertyBuilder<T> DefineProperty<T>(string name)
        => DefineProperty<T>(name, AccessModifier.Public);

    public PropertyBuilder<T> DefineProperty<T>(string name, AccessModifier accessModifier)
    {
        var pb = new PropertyBuilder<T>(this, name, accessModifier);
        _properties.Add(pb);
        return pb;
    }

    public MethodBuilder DefineMethod(string name)
        => DefineMethod(name, AccessModifier.Public);

    public MethodBuilder DefineMethod(string name, AccessModifier accessModifier, params IParameter[] parameters)
    {
        var mb = MethodBuilder.Action(this, name, accessModifier, parameters);
        _methods.Add(mb);
        return mb;
    }

    #endregion

    internal ClassDeclarationSyntax BuildClassDeclaration()
    {
        // Member group order: fields, properties, methods; within each group,
        // least protected first, then alphabetical.
        var members = new List<MemberDeclarationSyntax>();
        members.AddRange(GetMembers(_fields).Select(f => ((IMemberSyntaxBuilder)f).BuildMember()));
        members.AddRange(GetMembers(_properties).Select(p => ((IMemberSyntaxBuilder)p).BuildMember()));
        members.AddRange(GetMembers(_methods).Select(m => ((IMemberSyntaxBuilder)m).BuildMember()));

        return ClassDeclaration(Name)
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier, IsStatic, isPartial: IsPartial))
            .WithMembers(List(members));
    }

    public CompilationUnitSyntax BuildCompilationUnit()
    {
        var namespaceName = Namespace.BuildNameSyntax();

        MemberDeclarationSyntax namespaceDeclaration = IsFileScopedNamespace
            ? FileScopedNamespaceDeclaration(namespaceName)
                .WithMembers(SingletonList<MemberDeclarationSyntax>(BuildClassDeclaration()))
            : NamespaceDeclaration(namespaceName)
                .WithMembers(SingletonList<MemberDeclarationSyntax>(BuildClassDeclaration()));

        return CompilationUnit().WithMembers(SingletonList(namespaceDeclaration));
    }

    public SourceText ToSourceText()
        => SourceText.From(ToString(), Encoding.UTF8);

    internal override SyntaxNode BuildSyntax() => BuildCompilationUnit();

    private static void NameValidation(string name)
    {

    }

    // AccessabilityLevel runs Public = 0 through Private = 5, so ascending gives
    // least protected first.
    private static IEnumerable<TMember> GetMembers<TMember>(IEnumerable<TMember> members)
        where TMember : NamedBuilder, IAccessModifier, IMemberSyntaxBuilder
        => members.OrderBy(x => x.AccessModifier.AccessabilityLevel).ThenBy(x => x.Name, StringComparer.Ordinal);

    private ClassBuilder With(Action action)
    {
        action();
        return this;
    }
}
