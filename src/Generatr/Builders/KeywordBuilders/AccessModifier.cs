using Generatr.Abstractions;

namespace Generatr.Builders.KeywordBuilders;

public class AccessModifier : IBuilder
{
    private readonly IBuilder _keyword;

    private AccessModifier(IBuilder keyword, int accessabilityLevel)
    {
        _keyword = keyword;
        AccessabilityLevel = accessabilityLevel;
    }

    internal int AccessabilityLevel { get; }

    public void Build(TabbedBuilder tb) => _keyword.Build(tb);

    public static readonly AccessModifier Public = new(Keyword.Public, 0);
    public static readonly AccessModifier Internal = new(Keyword.Internal, 1);
    public static readonly AccessModifier Protected = new(Keyword.Protected, 2);
    public static readonly AccessModifier ProtectedInternal = new(Keyword.ProtectedInternal, 3);
    public static readonly AccessModifier PrivateProtected = new(Keyword.PrivateProtected, 4);
    public static readonly AccessModifier Private = new(Keyword.Private, 5);
}