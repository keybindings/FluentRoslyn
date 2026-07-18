using System.Collections.Generic;
using System.Linq;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

public class MethodBuilder : NamedBuilder, IAccessModifier, IMemberSyntaxBuilder
{
    private readonly ClassBuilder _classContext;
    private readonly TypeSyntax _returnType;
    private readonly IParameter[] _params;

    private MethodBuilder(ClassBuilder @class, string name, AccessModifier accessModifier, TypeSyntax returnType, IEnumerable<IParameter> @params) : base(name, _ => {})
    {
        _classContext = @class;
        AccessModifier = accessModifier;
        _returnType = returnType;
        _params = @params.ToArray();
    }

    public bool IsStatic { get; set; }

    public AccessModifier AccessModifier { get; set; }

    internal static MethodBuilder Action(ClassBuilder classContext, string name, AccessModifier accessModifier, IEnumerable<IParameter> @params)
        => new(classContext, name, accessModifier, PredefinedType(Token(SyntaxKind.VoidKeyword)), @params);

    internal MethodDeclarationSyntax BuildMethod()
        => MethodDeclaration(_returnType, Identifier(Name))
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier, IsStatic))
            .WithParameterList(ParameterList(SeparatedList(_params.Select(p =>
                SyntaxFactory.Parameter(Identifier(p.Name)).WithType(p.TypeName.BuildTypeSyntax())))))
            .WithBody(Block());

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildMethod();

    internal override SyntaxNode BuildSyntax() => BuildMethod();
}
