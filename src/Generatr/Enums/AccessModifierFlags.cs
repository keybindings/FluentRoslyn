using System;

namespace Generatr.Enums;

internal class AccessModifiersConstants
{
    public const string Public = "public";
    public const string Internal = "internal";
    public const string Protected = "protected";
    public const string Private = "private";
    public const string ProtectedInternal = "protected internal";
    public const string PrivateProtected = "private protected";
}


//public class FieldAccessModifier : StandardAccessModifier
//{

//    private FieldAccessModifier(string name)
//    {
//        Name = name;
//    }

//    public static readonly FieldAccessModifier ProtectedInternal = new(AccessModifiersConstants.ProtectedInternal);
//    public static readonly FieldAccessModifier PrivateProtected = new(AccessModifiersConstants.PrivateProtected);
//    public static readonly FieldAccessModifier Public = new(AccessModifiersConstants.Public);
//    public static readonly FieldAccessModifier Internal = new(AccessModifiersConstants.Internal);
//    public static readonly FieldAccessModifier Protected = new(AccessModifiersConstants.Protected);
//    public static readonly FieldAccessModifier Private = new(AccessModifiersConstants.Private);
//}

public class StandardAccessModifier
{
    protected StandardAccessModifier(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public static readonly StandardAccessModifier Public = new(AccessModifiersConstants.Public);
    public static readonly StandardAccessModifier Internal = new(AccessModifiersConstants.Internal);
    public static readonly StandardAccessModifier Protected = new(AccessModifiersConstants.Protected);
    public static readonly StandardAccessModifier Private = new(AccessModifiersConstants.Private);
}
