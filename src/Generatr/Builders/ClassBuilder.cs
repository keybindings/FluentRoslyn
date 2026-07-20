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
    private readonly List<ConstructorBuilder> _constructors = [];
    private readonly List<PropertyBuilder> _properties = [];
    private readonly List<MethodBuilder> _methods = [];
    private readonly List<AttributeSyntax> _attributes = [];

    internal ClassBuilder(NamespaceBuilder @namespace, string name) : base(name, NameValidation)
    {
        Namespace = @namespace;
    }

    public bool IsFileScopedNamespace { get; set; } = true;

    public bool IsStatic { get; set; }

    public bool IsPartial { get; set; }

    public NamespaceBuilder Namespace { get; }

    public AccessModifier AccessModifier { get; set; } = AccessModifier.Public;

    public ClassBuilder? ParentType { get; set; }

    #region FluentMethods

    public ClassBuilder Static() => With(() => IsStatic = true);

    public ClassBuilder Partial() => With(() => IsPartial = true);

    public ClassBuilder WithAccessModifier(AccessModifier accessModifier) => With(() => AccessModifier = accessModifier);

    public ClassBuilder BlockScopedNamespace() => With(() => IsFileScopedNamespace = false);

    public ClassBuilder WithParent(ClassBuilder type) => With(() => ParentType = type);

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("Serializable")</c>.</summary>
    public ClassBuilder WithAttribute(string attribute) => With(() => _attributes.Add(SyntaxAttributes.Attribute(attribute)));

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

    public ConstructorBuilder DefineConstructor()
        => DefineConstructor(AccessModifier.Public);

    public ConstructorBuilder DefineConstructor(AccessModifier accessModifier, params IParameter[] parameters)
    {
        var cb = new ConstructorBuilder(this, accessModifier, parameters);
        _constructors.Add(cb);
        return cb;
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
        => Add(MethodBuilder.Action(this, name, accessModifier, parameters));

    public MethodBuilder DefineMethod<TReturn>(string name)
        => DefineMethod<TReturn>(name, AccessModifier.Public);

    public MethodBuilder DefineMethod<TReturn>(string name, AccessModifier accessModifier, params IParameter[] parameters)
        => Add(MethodBuilder.Returning(this, name, accessModifier, TypeNameBuilder.New<TReturn>(), parameters));

    private MethodBuilder Add(MethodBuilder method)
    {
        _methods.Add(method);
        return method;
    }

    #endregion

    internal ClassDeclarationSyntax BuildClassDeclaration()
    {
        // Member group order: fields, properties, methods; within each group,
        // least protected first, then alphabetical.
        var members = new List<MemberDeclarationSyntax>();
        AddMemberGroup(members, _fields);
        AddMemberGroup(members, _constructors);
        AddMemberGroup(members, _properties);
        AddMemberGroup(members, _methods);

        var declaration = ClassDeclaration(Name)
            .WithAttributeLists(SyntaxAttributes.Lists(_attributes))
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier, IsStatic, isPartial: IsPartial))
            .WithMembers(List(members));

        return ParentType is { } parent
            ? declaration.WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(
                SimpleBaseType(parent.BuildTypeSyntax()))))
            : declaration;
    }

    public CompilationUnitSyntax BuildCompilationUnit()
    {
        // A class in the global namespace goes straight into the compilation unit;
        // there is no namespace declaration to wrap it in.
        if (Namespace.IsGlobal)
            return CompilationUnit().WithMembers(SingletonList<MemberDeclarationSyntax>(BuildClassDeclaration()));

        var namespaceName = Namespace.BuildNameSyntax();
        var classDeclaration = SingletonList<MemberDeclarationSyntax>(BuildClassDeclaration());

        MemberDeclarationSyntax namespaceDeclaration = IsFileScopedNamespace
            ? FileScopedNamespaceDeclaration(namespaceName).WithMembers(classDeclaration)
            : NamespaceDeclaration(namespaceName).WithMembers(classDeclaration);

        return CompilationUnit().WithMembers(SingletonList(namespaceDeclaration));
    }

    /// <summary>
    /// The fully qualified name of this class, for use as a type reference.
    /// </summary>
    internal TypeSyntax BuildTypeSyntax()
        => Namespace.IsGlobal
            ? IdentifierName(Name)
            : QualifiedName(Namespace.BuildNameSyntax(), IdentifierName(Name));

    public SourceText ToSourceText()
        => SourceText.From(ToString(), Encoding.UTF8);

    internal override SyntaxNode BuildSyntax() => BuildCompilationUnit();

    private static void NameValidation(string name)
    {

    }

    // AccessabilityLevel runs Public = 0 through Private = 5, so ascending gives
    // least protected first.
    private static void AddMemberGroup<TMember>(List<MemberDeclarationSyntax> members, IEnumerable<TMember> group)
        where TMember : NamedBuilder, IAccessModifier, IMemberSyntaxBuilder
        => members.AddRange(group
            .OrderBy(x => x.AccessModifier.AccessabilityLevel)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => x.BuildMember()));

    private ClassBuilder With(Action action)
    {
        action();
        return this;
    }
}
