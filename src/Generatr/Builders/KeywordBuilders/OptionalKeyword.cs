using Generatr.Abstractions;

namespace Generatr.Builders.KeywordBuilders;

internal class OptionalKeyword : INamedBuilder
{
    private readonly INamedBuilder _keyword;

    private OptionalKeyword(INamedBuilder keyword)
    {
        _keyword = keyword;
    }
    public string Name => _keyword.Name;

    public static OptionalKeyword Static => new(Keyword.Static);
    public static OptionalKeyword Partial => new(Keyword.Partial);
    public static OptionalKeyword Readonly => new(Keyword.Readonly);

    public bool IsSet { get; set; }

    public void Build(TabbedBuilder tb)
    {
        if (!IsSet) return;
        _keyword.Build(tb);
        tb.Space();
    }
}