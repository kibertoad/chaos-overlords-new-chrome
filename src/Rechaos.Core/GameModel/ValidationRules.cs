namespace Rechaos.Core.GameModel;

/// <summary>
/// One ordered, side-effect-free validation constraint. Returning null allows
/// the next rule to run; returning a typed failure stops the pipeline.
/// </summary>
public readonly record struct ValidationFailure<TCode>(TCode Code, string Message)
    where TCode : struct, Enum;

public interface IValidationRule<in TContext, TCode>
    where TCode : struct, Enum
{
    ValidationFailure<TCode>? Evaluate(TContext context);
}

public static class ValidationRuleSet
{
    public static ValidationFailure<TCode>? Evaluate<TContext, TCode>(
        TContext context,
        IEnumerable<IValidationRule<TContext, TCode>> rules)
        where TCode : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var rule in rules)
        {
            ArgumentNullException.ThrowIfNull(rule);
            if (rule.Evaluate(context) is { } failure) return failure;
        }
        return null;
    }
}
