using System;
using System.Collections.Generic;
using System.Text;
using Generatr.Enums;

namespace Generatr.Builders;

public class ClassBuilder : Builder
{
    internal ClassBuilder(NamespaceBuilder @namespace, string name, AccessModifiers accessModifier) : base(name)
    {
        Namespace = @namespace;
        AccessModifier = accessModifier;
    }

    public NamespaceBuilder Namespace { get; set; }
    public AccessModifiers AccessModifier { get; }

    public bool IsPartial { get; }
    public bool UseFileScopedNamespace { get; set; } = true;

    public ClassBuilder ParentType { get; set; }

    public HashSet<string> Usings { get; set; }

    public ClassBuilder SetBaseType(ClassBuilder type)
        => ParentType = type;


    #region Fields
    public FieldBuilder AddPublicField(ClassBuilder type, string name)
        => AddField(type, name, AccessModifiers.Public);
    public FieldBuilder AddPrivateField(ClassBuilder type, string name) =>
        AddField(type, name, AccessModifiers.Private);
    public FieldBuilder AddField(ClassBuilder type, string name, AccessModifiers accessModifier) =>
        new(this, type, name, accessModifier);

    #endregion

    #region Properties

    //public PropertyBuilder AddGetSetPropertyField(ClassBuilder type, string name)
    //    => AddField(this, type, name, AccessModifiers.Public);

    #endregion

    protected override string Build()
    {
        var sb = new StringBuilder();
        // Grab all usings from base type, fields, properties, and types used within methods

        // Build those

        // Build Namespace
        sb.Append(Namespace);
        sb.AppendLine(Environment.NewLine);

        // Write all fields in order of: most protected to least protected, then alphabetical

        // Write Constructors

        // Write Properties order of: most protected to least protected, then alphabetical

        // Write Methods order of: most protected to least protected, then alphabetical

        return sb.ToString();
    }

}