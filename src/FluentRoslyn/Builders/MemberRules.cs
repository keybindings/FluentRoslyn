using System;
using System.Collections.Generic;
using System.Linq;
using FluentRoslyn.Abstractions;

namespace FluentRoslyn.Builders;

/// <summary>
/// The rules C# imposes on a member because of the <em>type</em> it is declared in, and
/// on a set of members because of each other. Only the type knows both halves, so they
/// live here rather than on any member builder.
/// </summary>
/// <remarks>
/// Review 3 found six of these missing at once, which says the gap was the concept
/// rather than the cases: <c>BuildMembers</c> validated abstract methods and operators
/// and nothing else, while <c>IsStaticType</c> existed and was consulted for exactly one
/// member kind. Each rule below replaces source that failed in the consumer's build.
/// </remarks>
internal sealed class MemberRules(
    string typeName,
    TypeKind kind,
    bool isStaticType,
    bool isReadonlyType,
    bool isPartialType,
    bool allowsAbstractMembers)
{
    /// <summary>Refuses anything the declaring type forbids, before a line is emitted.</summary>
    internal void Validate(
        IReadOnlyCollection<FieldBuilder> fields,
        IReadOnlyCollection<ConstructorBuilder> constructors,
        IReadOnlyCollection<EventBuilder> events,
        IReadOnlyCollection<PropertyBuilder> properties,
        IReadOnlyCollection<IMethodMember> methods,
        IReadOnlyCollection<TypeDeclarationBuilder> nestedTypes,
        bool hasInterfaces)
    {
        ValidateAbstract(methods);
        ValidateStaticType(fields, constructors, events, properties, methods, hasInterfaces);
        ValidateReadonlyStruct(fields, properties);
        ValidateStructInitializers(fields, constructors);
        ValidateInheritanceModifiers(methods);
        ValidateNames(fields, events, properties, methods, nestedTypes);
    }

    // An abstract member in a non-abstract type does not compile. Pre-existing; moved
    // here so every member rule reads in one place.
    private void ValidateAbstract(IReadOnlyCollection<IMethodMember> methods)
    {
        if (!allowsAbstractMembers && methods.FirstOrDefault(m => m.IsAbstract) is { } abstractMethod)
            throw new InvalidOperationException(
                $"Type '{typeName}' declares abstract method '{abstractMethod.Name}' but is not abstract.");
    }

    // CS0708 for an instance member, CS0710 for an instance constructor, CS0714 for an
    // implemented interface. A static class is a bag of static members and nothing else.
    private void ValidateStaticType(
        IReadOnlyCollection<FieldBuilder> fields,
        IReadOnlyCollection<ConstructorBuilder> constructors,
        IReadOnlyCollection<EventBuilder> events,
        IReadOnlyCollection<PropertyBuilder> properties,
        IReadOnlyCollection<IMethodMember> methods,
        bool hasInterfaces)
    {
        if (!isStaticType)
            return;

        if (fields.FirstOrDefault(f => !f.IsStatic && !f.IsConst) is { } field)
            throw StaticTypeMember("field", field.Name);

        if (properties.FirstOrDefault(p => !p.IsStatic) is { } property)
            throw StaticTypeMember("property", property.Name);

        if (methods.FirstOrDefault(m => !m.IsStaticMember) is { } method)
            throw StaticTypeMember("method", method.Name);

        if (events.FirstOrDefault(e => !e.IsStatic) is { } @event)
            throw StaticTypeMember("event", @event.Name);

        // A static constructor is legal and is what a static class uses; only an
        // instance one is refused.
        if (constructors.FirstOrDefault(c => !c.IsStatic) is not null)
            throw new InvalidOperationException(
                $"Static class '{typeName}' cannot declare an instance constructor. Use a static " +
                "constructor, or make the type non-static.");

        if (hasInterfaces)
            throw new InvalidOperationException(
                $"Static class '{typeName}' cannot implement an interface: no instance can exist to " +
                "satisfy it.");
    }

    private InvalidOperationException StaticTypeMember(string memberKind, string memberName)
        => new($"Static class '{typeName}' cannot declare the instance {memberKind} '{memberName}'. " +
               $"Mark it static, or make the type non-static.");

    // CS8340 and CS8341: every instance field of a readonly struct must be readonly, and
    // an auto-property must not have a setter. Both are the *default* shapes of
    // DefineField and DefineProperty, so a readonly struct is broken by taking defaults.
    private void ValidateReadonlyStruct(
        IReadOnlyCollection<FieldBuilder> fields,
        IReadOnlyCollection<PropertyBuilder> properties)
    {
        if (!isReadonlyType)
            return;

        if (fields.FirstOrDefault(f => !f.IsReadonly && !f.IsStatic && !f.IsConst) is { } field)
            throw new InvalidOperationException(
                $"Readonly struct '{typeName}' cannot declare the mutable instance field " +
                $"'{field.Name}'. Call Readonly() on it, or make it static.");

        if (properties.FirstOrDefault(p => p.IsAutoProperty && p.HasSet && !p.IsStatic) is { } property)
            throw new InvalidOperationException(
                $"Readonly struct '{typeName}' cannot declare the settable auto-property " +
                $"'{property.Name}'. Call GetOnly() on it, or give it an explicit body.");
    }

    // CS8983: a struct with a field initializer needs an explicitly declared constructor.
    private void ValidateStructInitializers(
        IReadOnlyCollection<FieldBuilder> fields,
        IReadOnlyCollection<ConstructorBuilder> constructors)
    {
        if (kind != TypeKind.Struct || constructors.Any(c => !c.IsStatic))
            return;

        if (fields.FirstOrDefault(f => f.Initializer is not null && !f.IsStatic && !f.IsConst) is { } field)
            throw new InvalidOperationException(
                $"Struct '{typeName}' initializes the instance field '{field.Name}', so it must also " +
                "declare a constructor. C# requires one when any instance field has an initializer.");
    }

    // CS0549 (virtual in a sealed type), CS0106 (virtual on a struct), CS0751 (partial
    // member outside a partial type). The abstract rule above already had this shape.
    private void ValidateInheritanceModifiers(IReadOnlyCollection<IMethodMember> methods)
    {
        if (methods.FirstOrDefault(m => m.IsVirtual) is { } virtualMethod &&
            (kind == TypeKind.Struct || !allowsAbstractMembers))
            throw new InvalidOperationException(
                $"Type '{typeName}' declares virtual method '{virtualMethod.Name}', which needs a type " +
                "that can be derived from. A struct and a sealed or static class cannot.");

        if (!isPartialType && methods.FirstOrDefault(m => m.IsPartialMember) is { } partialMethod)
            throw new InvalidOperationException(
                $"Type '{typeName}' declares partial method '{partialMethod.Name}' but is not partial.");
    }

    // CS0102 (two members of one name), CS0111 (two methods of one signature), CS0542 (a
    // member named after its enclosing type). EnumBuilder has done this for its own
    // members since Review 1; type members never got it.
    private void ValidateNames(
        IReadOnlyCollection<FieldBuilder> fields,
        IReadOnlyCollection<EventBuilder> events,
        IReadOnlyCollection<PropertyBuilder> properties,
        IReadOnlyCollection<IMethodMember> methods,
        IReadOnlyCollection<TypeDeclarationBuilder> nestedTypes)
    {
        // Fields, properties, events and nested types share one namespace and collide on
        // the name alone; methods collide only on name *and* parameter types, since
        // overloads are legal.
        var byName = fields.Select(f => f.Name)
            .Concat(properties.Select(p => p.Name))
            .Concat(events.Select(e => e.Name))
            .Concat(nestedTypes.Select(t => t.Name));

        if (FirstDuplicate(byName) is { } duplicate)
            throw new InvalidOperationException(
                $"Type '{typeName}' declares more than one member named '{duplicate}'.");

        if (FirstDuplicate(methods.Select(m => $"{m.Name}({m.ParameterSignature})")) is { } method)
            throw new InvalidOperationException(
                $"Type '{typeName}' declares the method '{method}' more than once. Overloads must " +
                "differ in their parameter types.");

        // A member may not share its enclosing type's name -- the constructor owns it.
        var shadowing = byName.Concat(methods.Select(m => m.Name))
            .FirstOrDefault(n => string.Equals(n, typeName, StringComparison.Ordinal));

        if (shadowing is not null)
            throw new InvalidOperationException(
                $"Type '{typeName}' declares a member named '{shadowing}', which is its own name. C# " +
                "reserves that for the constructor.");
    }

    private static string? FirstDuplicate(IEnumerable<string> names)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return names.FirstOrDefault(n => !seen.Add(n));
    }
}

/// <summary>Which kind of type a builder emits, for the rules that differ by kind.</summary>
internal enum TypeKind
{
    Class,
    Struct,
}
