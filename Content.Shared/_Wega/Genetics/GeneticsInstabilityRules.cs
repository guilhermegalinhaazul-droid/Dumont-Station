using System.Collections.Generic;

namespace Content.Shared.Genetics;

/// <summary>
/// Single set of thresholds used by both Wega structural genes and Trauma
/// mutations. Keeping the thresholds here prevents the two genetics systems
/// from drifting apart.
/// </summary>
public static class GeneticsInstabilityRules
{
    public const int WarningThreshold = 20;
    public const int StageOneThreshold = 35;
    public const int StageTwoThreshold = 65;
    public const int MeltdownThreshold = 100;

    public static int Add(int total, int cost) => total + cost;

    public static int Sum(IEnumerable<int> costs)
    {
        var total = 0;
        foreach (var cost in costs)
            total = Add(total, cost);
        return total;
    }

    public static int Stage(int total) => total switch
    {
        > StageTwoThreshold => 3,
        > StageOneThreshold => 2,
        > WarningThreshold => 1,
        _ => 0
    };

    public static bool IsMelting(int total, int maximum = MeltdownThreshold)
        => total >= maximum;
}
