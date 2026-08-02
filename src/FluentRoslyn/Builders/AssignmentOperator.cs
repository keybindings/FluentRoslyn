namespace FluentRoslyn.Builders;

/// <summary>
/// The compound assignment operators, for <c>Assign(target, op, value)</c>.
/// </summary>
/// <remarks>
/// Modelled as an enum rather than one method per operator: ten named pairs would be a
/// lot of surface for operators that mostly differ by a token. <c>??=</c> is deliberately
/// absent — it needs a nullable target, which is a constraint the shared signature cannot
/// express, so it has its own method (<c>AssignIfNull</c>) that can state it.
/// </remarks>
public enum AssignmentOperator
{
    /// <summary><c>+=</c></summary>
    Add,

    /// <summary><c>-=</c></summary>
    Subtract,

    /// <summary><c>*=</c></summary>
    Multiply,

    /// <summary><c>/=</c></summary>
    Divide,

    /// <summary><c>%=</c></summary>
    Modulo,

    /// <summary><c>&amp;=</c></summary>
    And,

    /// <summary><c>|=</c></summary>
    Or,

    /// <summary><c>^=</c></summary>
    ExclusiveOr,

    /// <summary><c>&lt;&lt;=</c></summary>
    LeftShift,

    /// <summary><c>&gt;&gt;=</c></summary>
    RightShift,
}
