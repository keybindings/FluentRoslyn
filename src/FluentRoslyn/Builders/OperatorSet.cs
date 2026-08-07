using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FluentRoslyn.Builders;

/// <summary>
/// The operators a type declares, with the validation C# imposes across all of them.
/// Shared by every type builder that can carry operators, so the rules exist once and a
/// list can never hold something the validation silently skips.
/// </summary>
/// <remarks>
/// Everything here refuses to emit rather than emitting source the consumer's compiler
/// rejects — a build error in someone else's project, pointing at generated code they
/// did not write. The signature model all rules share: two declarations are the same
/// overload when their kind and canonical parameter types agree; C# matches pairs and
/// twins by signature, not by symbol.
/// </remarks>
internal sealed class OperatorSet
{
    private readonly List<IOperatorMember> _operators = [];

    internal TOperator Add<TOperator>(TOperator @operator) where TOperator : IOperatorMember
    {
        _operators.Add(@operator);
        return @operator;
    }

    /// <summary>Builds every operator, in declaration order.</summary>
    /// <remarks>
    /// Declaration order rather than the sorted order other member groups use: operators
    /// have no access modifier to sort by, and alphabetical order would put
    /// <c>operator !=</c> before <c>operator ==</c>, splitting a pair the language
    /// requires together. Declaration order is still deterministic, because the
    /// generator chooses it.
    /// </remarks>
    internal void AppendMembers(List<MemberDeclarationSyntax> members)
        => members.AddRange(_operators.Select(o => o.BuildMember()));

    internal void Validate(string typeName, bool isStaticType)
    {
        // The common case is no operators at all, and it should cost nothing.
        if (_operators.Count == 0)
            return;

        // CS0715: a static class cannot contain user-defined operators (its type can
        // never be a parameter, CS0721). Only the type knows it is static, so the check
        // lives here, beside the abstract-member check's reasoning.
        if (isStaticType)
            throw new InvalidOperationException(
                $"Type '{typeName}' is a static class, which cannot declare operators: its type " +
                "can never appear as a parameter or result.");

        var context = $"Type '{typeName}'";
        foreach (var @operator in _operators)
            @operator.ValidateMember(context);

        ValidateOperatorGroups(typeName);
        ValidateConversionGroups(typeName);
        ValidatePairs(typeName);
    }

    // Within one overload -- same kind, same parameter signature -- C# allows exactly
    // one unchecked form and, where eligible, one checked form that requires it
    // (CS0111 for duplicates, CS9025 for a checked form alone).
    private void ValidateOperatorGroups(string typeName)
    {
        foreach (var group in _operators.Where(o => o.Kind is not null)
                     .GroupBy(o => (o.Kind, o.ParameterSignature)))
        {
            var normal = group.Count(o => !o.IsChecked);
            var check = group.Count(o => o.IsChecked);

            if (normal > 1 || check > 1)
                throw new InvalidOperationException(
                    $"Type '{typeName}' declares '{group.First().Display}' with parameter types " +
                    $"{group.Key.ParameterSignature} more than once.");

            foreach (var @operator in group.Where(o => o.IsChecked && !o.PartnerElsewhere))
                if (normal == 0)
                    throw new InvalidOperationException(
                        $"Type '{typeName}' declares checked '{@operator.Display}' without a matching " +
                        $"unchecked form taking the same parameter types {@operator.ParameterSignature}. " +
                        "C# requires both.");
        }
    }

    // Conversion identity ignores implicit-versus-explicit: CS0557 rejects declaring
    // both directions between the same types, however they are decorated. Within one
    // identity the only legal shapes are a lone conversion or an explicit
    // checked/unchecked twin pair.
    private void ValidateConversionGroups(string typeName)
    {
        foreach (var group in _operators.Where(o => o.Conversion is not null)
                     .GroupBy(o => (o.ParameterSignature, o.ResultTypeText)))
        {
            var kinds = group.Select(o => o.Conversion!.Value).Distinct().Count();
            if (kinds > 1)
                throw new InvalidOperationException(
                    $"Type '{typeName}' declares both an implicit and an explicit conversion from " +
                    $"{group.Key.ParameterSignature} to '{group.Key.ResultTypeText}'. C# forbids " +
                    "declaring both directions between the same types.");

            var normal = group.Count(o => !o.IsChecked);
            var check = group.Count(o => o.IsChecked);

            if (normal > 1 || check > 1)
                throw new InvalidOperationException(
                    $"Type '{typeName}' declares the conversion from {group.Key.ParameterSignature} to " +
                    $"'{group.Key.ResultTypeText}' more than once.");

            foreach (var conversion in group.Where(o => o.IsChecked && !o.PartnerElsewhere))
                if (normal == 0)
                    throw new InvalidOperationException(
                        $"Type '{typeName}' declares checked '{conversion.Display}' without a matching " +
                        "unchecked form. C# requires both.");
        }
    }

    // CS0216: == needs !=, the ordering pairs need each other, true needs false -- and
    // the partner must take the same parameter types, not merely exist somewhere in the
    // type. Checked forms never participate: comparison operators have no checked form,
    // and a checked arithmetic form is paired through the twin rule instead.
    private void ValidatePairs(string typeName)
    {
        var declared = new HashSet<(OperatorKind, string)>(
            _operators.Where(o => o.Kind is not null && !o.IsChecked)
                .Select(o => (o.Kind!.Value, o.ParameterSignature)));

        foreach (var @operator in _operators.Where(o => o.Kind is not null && !o.IsChecked && !o.PartnerElsewhere))
        {
            var partner = Operators.PartnerOf(@operator.Kind!.Value);
            if (partner is not null && !declared.Contains((partner.Value, @operator.ParameterSignature)))
                throw new InvalidOperationException(
                    $"Type '{typeName}' declares operator '{Operators.SymbolFor(@operator.Kind.Value)}' without " +
                    $"'{Operators.SymbolFor(partner.Value)}'. C# requires the pair, and the partner must take " +
                    $"the same parameter types {@operator.ParameterSignature}.");
        }
    }
}
