using System;
using System.Collections.Generic;
using System.Linq;
using Generatr.Abstractions;
using Generatr.Builders.KeywordBuilders;

namespace Generatr.Builders;

public class ClassBuilder : NamedBuilder
{
    private readonly HashSet<string> _memberNames = new();
    private readonly List<FieldBuilder> _fields = [];
    private readonly List<PropertyBuilder> _properties = new();
    private readonly List<MethodBuilder> _methods = new();
    private readonly OptionalKeyword _staticBuilder = OptionalKeyword.Static;
    private readonly OptionalKeyword _partialBuilder = OptionalKeyword.Partial;
    internal ClassBuilder(NamespaceBuilder @namespace, string name) : base(name, NameValidation)
    {
        Namespace = @namespace;
    }

    public bool IsFileScopedNamespace { get; set; } = true;

    public bool IsStatic { get => _staticBuilder.IsSet; set => _staticBuilder.IsSet = value; }

    public bool IsPartial { get => _partialBuilder.IsSet; set => _partialBuilder.IsSet = value; }

    // public bool IsGeneric { get; private set; }

    public NamespaceBuilder Namespace { get; }

    public AccessModifier AccessModifier { get; set; } = AccessModifier.Public;

    public ClassBuilder ParentType { get; set; }

    #region FluentMethods

    public ClassBuilder Static() => With(() => IsStatic = true);

    public ClassBuilder Partial() => With(() => IsPartial = true);

    public ClassBuilder WithAccessModifier(AccessModifier accessModifier) => With(() => AccessModifier = accessModifier);

    public ClassBuilder BlockScopedNamespace() => With(() => IsFileScopedNamespace = false);

    public ClassBuilder WithParent(ClassBuilder type) => With(() => ParentType = type);
    #endregion
    
    #region Fields
        
    public FieldBuilder<T> DefineField<T>(string name)
        => DefineField<T>(name, AccessModifier.Private);

    public FieldBuilder<T> DefineField<T>(string name, AccessModifier accessModifierFlags)
    {
        var fb = new FieldBuilder<T>(this, name, accessModifierFlags);
        _fields.Add(fb);
        return fb;
    }

    #endregion

    #region Properties

    //public PropertyBuilder DefineProperty<T>(string name)
    //    => DefineProperty<T>(this, name, AccessModifier.Public);


    #endregion

    public override void Build(TabbedBuilder tb)
    {
        // TODO Complete usings
        // TODO Update, don't care about usings, will use the full definitions always to not confuse using statements and require specifying definitions
        // TODO If we care about usings later on we can look to collect common usings however that's way off for now
        // Grab all usings from base type, fields, properties, and types used within methods

        // Build those

        // Build Namespace
        Keyword.Namespace.Build(tb);
        tb.Space();
        Namespace.Build(tb);
        if (IsFileScopedNamespace)
        {
            tb.SemiColon();
            tb.NewLine();
        }
        else
        {
            tb.NewLine();
            tb.Open();
        }

        // Write Class Definition
        AccessModifier.Build(tb);
        tb.Space();
        _staticBuilder.Build(tb);
        _partialBuilder.Build(tb);
        Keyword.Class.Build(tb);
        tb.Space();
        base.Build(tb);

        tb.NewLine().Open();

        //if(_fields.Count > 0) tb.NewLine();

        // Write all fields in order of: least protected to most protected, then alphabetical

        foreach (var field in GetMembers(_fields))
        {
            field.Build(tb);
        }

        // Write Constructors

        // Write Properties order of: lease protected to most protected, then alphabetical

        // Write Methods order of: least protected to most protected, then alphabetical


        // Close Class
        tb.Close();

        // Close Namespace
        if (!IsFileScopedNamespace)
            tb.Close();

    }

    private static void NameValidation(string name)
    {

    }

    private IEnumerable<TMember> GetMembers<TMember>(IEnumerable<TMember> members) where TMember : NamedBuilder, IAccessModifier
        => members.OrderByDescending(x => x.AccessModifier).ThenBy(x => x.Name);

    private ClassBuilder With(Action action)
    {
        action();
        return this;
    }
}