using System;
using System.Text;

namespace Generatr.Builders;

public class TabbedBuilder(StringBuilder sb)
{
    public TabbedBuilder() : this(new StringBuilder())
    {
    }
    private int _indentation;

    public TabbedBuilder Space() => Append(' ');
    public TabbedBuilder SemiColon() => Append(';');

    public TabbedBuilder Open()
        => OpenBracket().Tab().NewLine();

    public TabbedBuilder Close()
        => UnTab().NewLine().CloseBracket();

    public TabbedBuilder OpenBracket() => Append('{');

    public TabbedBuilder CloseBracket() => Append('}');


    public TabbedBuilder OpenRoundBracket() => Append('(');
    public TabbedBuilder CloseRoundBracket() => Append(')');

    public TabbedBuilder OpenAngleBracket() => Append('<');
    
    public TabbedBuilder CloseAngleBracket() => Append('>');
    public TabbedBuilder Period() => Append('.');

    public TabbedBuilder Comma() => Append(',');
    public TabbedBuilder Append(char val)
        => ActionReturn(() => sb.Append(val));

    public TabbedBuilder Append(string val)
        => ActionReturn(() => sb.Append(val));

    public TabbedBuilder NewLine()
        => ActionReturn(() =>
        {
            sb.Append(Environment.NewLine);
            for (var i = 0; i < _indentation; i++)
            {
                sb.Append('\t');
            }
        });
    public override string ToString()
    {
        for (var i = 0; i < _indentation; i++) Close();
        return sb.ToString();
    }

    //public void AppendStatement(StringBuilder statementBuilder)
    //{
    //    AppendStatement(statementBuilder.ToString());
    //}
    //public void AppendStatement(string statement)
    //{
    //    Append(statement);
    //}

    //public void AppendStatementLine(string statement)
    //{
    //    AppendStatement(statement);
    //    sb.Append(Environment.NewLine);
    //}

    //public void AppendLine(string val) => sb.AppendLine(val);

    //public void InsertStart(char val) => sb.Insert(0, val);

    //public void InsertStart(string val) => sb.Insert(0, val);

    //public void Insert(int index, char val) => sb.Insert(index, val);

    private TabbedBuilder Tab() => ActionReturn(() => _indentation++);

    private TabbedBuilder UnTab() => ActionReturn(() => _indentation--);

    private TabbedBuilder ActionReturn(Action action)
    {
        action();
        return this;
    }
}