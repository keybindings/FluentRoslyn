using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Generatr.Abstractions;

internal interface IMemberSyntaxBuilder
{
    MemberDeclarationSyntax BuildMember();
}
