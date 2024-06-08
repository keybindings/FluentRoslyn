using System;
using Generatr.Builders;

namespace Generatr.Helpers;

internal static class TypeExtensions
{
    public static ClassBuilder ToClassBuilder(this Type type)
    {
        var namespaceBuilder = NamespaceBuilder.Get(type.Namespace);
        return namespaceBuilder.Class(type.Name);
    }
}