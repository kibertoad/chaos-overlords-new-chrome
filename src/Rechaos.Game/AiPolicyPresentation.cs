using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class AiPolicyPresentation
{
    public static string Label(AiPolicyMode policy) => policy switch
    {
        AiPolicyMode.Original => "ORIGINAL",
        AiPolicyMode.Advanced => "ADVANCED",
        _ => throw new ArgumentOutOfRangeException(nameof(policy))
    };
}
