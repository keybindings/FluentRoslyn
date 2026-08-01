using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FluentRoslyn.Abstractions;

internal interface IMemberSyntaxBuilder
{
    MemberDeclarationSyntax BuildMember();
}
