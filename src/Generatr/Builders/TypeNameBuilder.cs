using System;
using System.Collections.Generic;
using System.Linq;
using Generatr.Abstractions;

namespace Generatr.Builders;

public class TypeNameBuilder : NamedBuilder
{
    private readonly NamespaceBuilder _namespaceBuilder;
    private readonly List<TypeNameBuilder> _genericTypes = [];

    // private TypeNameBuilder(ClassBuilder classBuilder) : base(classBuilder.Name)
    // {
    //     _namespaceBuilder = classBuilder.Namespace;
    // }
    // private TypeNameBuilder(Type type) : base(GetTypeName(type), NameValidation)
    // {
    //     var genericArgs = type.GenericTypeArguments;
    //     _namespaceBuilder = NamespaceBuilder.Get(type.Namespace);
    //     foreach (var gType in genericArgs)
    //     {
    //         _genericTypes.Add(new TypeNameBuilder(gType));
    //     }
    // }

    private TypeNameBuilder(string name, NamespaceBuilder namespaceBuilder) : base(name, NameValidation)
    {
        _namespaceBuilder = namespaceBuilder;
    }

    public static TypeNameBuilder New<T>()
        => New(typeof(T));
    
    private static TypeNameBuilder New(Type type)
    {
        var tb = NewEmptyBuilder(type);
        
        foreach (var genericArg in type.GenericTypeArguments)
        {
            tb._genericTypes.Add(New(genericArg));
        }

        return tb;
    }
    
    public bool IsGenericType { get; private set; }

    public int GenericArgumentsCount { get; private set; } = 0;

    public List<string> GenericArgsNames = new();
    public override void Build(TabbedBuilder tb)
    {
        if (_namespaceBuilder != NamespaceBuilder.None)
        {
            _namespaceBuilder.Build(tb);
            tb.Period();
        }
        
        tb.Append(Name);
        if (!_genericTypes.Any()) return;
        tb.OpenAngleBracket();
        for (var i = 0; i < _genericTypes.Count; i++)
        {
            if (i > 0 && i < _genericTypes.Count - 1)
            {
                tb.Comma();
            }

            _genericTypes[i].Build(tb);
        }

        tb.CloseAngleBracket();
    }


    private static TypeNameBuilder NewEmptyBuilder(Type type)
    {
        if (TryUseShorthandName(type, out var shortName))
        {
            return new TypeNameBuilder(shortName, NamespaceBuilder.None);
        }
        var typeName = new string(type.Name.TakeWhile(c => c != '`').ToArray());

        return new TypeNameBuilder(typeName, NamespaceBuilder.Get(type.Namespace));
    }
    private static bool TryUseShorthandName(Type type, out string shortName)
    {
        if (type.Namespace != "System")
        {
            shortName = string.Empty;
            return false;
        }

        var name = type.Name;
        
        shortName = name switch
        {
            "String" => "string",
            "Boolean" => "bool",
            "Int32" => "int",
            "Double" => "double",
            "Single" => "float",
            _ => string.Empty
        };

        return shortName != string.Empty;
    }

    private static void NameValidation(string name)
    {

    }
}