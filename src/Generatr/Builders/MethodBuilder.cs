using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Generatr.Abstractions;
using Generatr.Builders.KeywordBuilders;

namespace Generatr.Builders;

public class TestMock<T> : IBuilder where T : class
{
    public TestMock()
    {
        var method = typeof(T).GetMethods(BindingFlags.Public);

    }
    public void Build(TabbedBuilder tb)
    {
        throw new System.NotImplementedException();
    }
}
public class MethodStatements : IBuilder
{
    public void Build(TabbedBuilder tb)
    {
        throw new System.NotImplementedException();
    }
}

//public class AssignStatement<T> : AssignStatement
//{
//}


public class Statement : IBuilder
{
    public void Build(TabbedBuilder tb)
    {
        throw new System.NotImplementedException();
    }
}
public class MethodBuilder : NamedBuilder
{
    private readonly ClassBuilder _classContext;
    private readonly AccessModifier _accessModifier;
    private readonly IBuilder _returnBuilder;
    private readonly IBuilder _methodStatements;
    private readonly IParameter[] _params;
    private readonly OptionalKeyword _staticBuilder = OptionalKeyword.Static;
    private readonly MethodContextBuilder _context = new();

    private MethodBuilder(ClassBuilder @class, string name, AccessModifier accessModifier, IBuilder returnBuilder, IEnumerable<IParameter> @params, IBuilder methodStatements) : base(name, _ => {})
    {
        _classContext = @class;
        _accessModifier = accessModifier;
        _returnBuilder = returnBuilder;
        _methodStatements = methodStatements;
        _params = @params.ToArray();
    }

    public bool IsStatic { get => _staticBuilder.IsSet; set => _staticBuilder.IsSet = value; }

    internal static MethodBuilder Action(ClassBuilder classContext, string name, AccessModifier accessModifier, IEnumerable<IParameter> @params)
    {
        return new MethodBuilder(classContext, name, accessModifier, Keyword.Void, @params, null);
    }
    

    public override void Build(TabbedBuilder tb)
    {
        _accessModifier.Build(tb);
        _staticBuilder.Build(tb);
        _returnBuilder.Build(tb);
        base.Build(tb);
        tb.OpenRoundBracket();
        for (var i = 0; i < _params.Length; i++)
        {
            _params[i].Build(tb);
            if (i < _params.Length - 1) continue;
            tb.Comma().Space();
        }
        tb.CloseRoundBracket().NewLine().Open();
        _methodStatements.Build(tb);
        tb.NewLine().Close();
    }
}