using Generatr.Abstractions;

namespace Generatr.Builders.KeywordBuilders;

//[Flags]
//public enum ModifierFlags
//{

//}

internal class Keyword : NamedBuilder
{
    // Declarations
    private const string NamespaceDefinition = "namespace";
    private const string ClassDefinition = "class";

    // Access Modifiers
    private const string PublicDefinition = "public";
    private const string InternalDefinition = "internal";
    private const string ProtectedDefinition = "protected";
    private const string PrivateDefinition = "private";
    private const string ProtectedInternalDefinition = "protected internal";
    private const string PrivateProtectedDefinition = "private protected";

    // Optional
    private const string StaticDefinition = "static";
    private const string PartialDefinition = "partial";
    private const string ReadonlyDefinition = "readonly";

    // Properties
    private const string GetDefinition = "get";
    private const string SetDefinition = "set";
    private const string ValueDefinition = "value";
    private const string ConstDefinition = "const";

    // Methods
    private const string VoidDefinition = "void";
    private const string ReturnDefinition = "return";

    private Keyword(string name) : base(name, _ => {})
    {
    }

    public static readonly Keyword Namespace = new(NamespaceDefinition);
    public static readonly Keyword Class = new(ClassDefinition);
    public static readonly Keyword Public = new(PublicDefinition);
    public static readonly Keyword Internal = new(InternalDefinition);
    public static readonly Keyword Protected = new(ProtectedDefinition);
    public static readonly Keyword Private = new(PrivateDefinition);
    public static readonly Keyword ProtectedInternal = new(ProtectedInternalDefinition);
    public static readonly Keyword PrivateProtected = new(PrivateProtectedDefinition);
    public static readonly Keyword Static = new(StaticDefinition);
    public static readonly Keyword Partial = new(PartialDefinition);
    public static readonly Keyword Const = new(ConstDefinition);
    public static readonly Keyword Readonly = new(ReadonlyDefinition);
    public static readonly Keyword Get = new(GetDefinition);
    public static readonly Keyword Set = new(SetDefinition);
    public static readonly Keyword Value = new(ValueDefinition);
    public static readonly Keyword Void = new(VoidDefinition);
    public static readonly Keyword Return = new (ReturnDefinition);
}