// SPDX-License-Identifier: AGPL-3.0-or-later
// Base pairing and strand layout adapted from Trauma's MutationData.
using System.Linq;
using Content.Shared.UserInterface;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Shared.Genetics;

public static class GeneticSequence
{
    public const int StructuralPairs = 12;
    public const int AppearancePairs = 12;
    public static readonly char[] Bases = { 'A', 'T', 'G', 'C' };

    /// <summary>
    /// Converts the legacy 32-character forensic DNA identifier into the
    /// nitrogen-base alphabet used by the genetics console. The identifier
    /// remains unchanged; this is only its stable display representation.
    /// </summary>
    public static string ToNitrogenBases(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (value.All(c => Array.IndexOf(Bases, char.ToUpperInvariant(c)) >= 0))
            return value.ToUpperInvariant();

        var result = new char[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            var c = char.ToUpperInvariant(value[i]);
            result[i] = c is >= '0' and <= '9'
                ? Bases[(c - '0') % Bases.Length]
                : c is >= 'A' and <= 'F'
                    ? Bases[(c - 'A' + 10) % Bases.Length]
                    : 'N';
        }

        return new string(result);
    }
    public static char Matching(char value) => value switch
    {
        'A' => 'T', 'T' => 'A', 'G' => 'C', 'C' => 'G', _ => 'X'
    };

    public static string Generate(IRobustRandom random, int pairs)
    {
        var bases = new char[pairs * 2];
        for (var i = 0; i < pairs; i++)
        {
            bases[i] = random.Pick(Bases);
            bases[i + pairs] = Matching(bases[i]);
        }
        return new string(bases);
    }

    public static string HideAppearance(IRobustRandom random, string sequence)
    {
        var bases = sequence.ToCharArray();
        var pairs = bases.Length / 2;
        for (var i = 0; i < pairs; i++)
            bases[i + (random.Prob(0.5f) ? pairs : 0)] = 'X';
        return new string(bases);
    }

    public static string HideStructural(IRobustRandom random, string sequence, int difficulty)
    {
        var bases = sequence.ToCharArray();
        var remaining = Math.Clamp(difficulty + random.Next(-2, 2), 2, bases.Length);
        HidePositions(random, bases, remaining);
        return new string(bases);
    }

    /// <summary>
    /// Creates the easier puzzle used for a gene that is already active.
    /// Exactly three or four positions are hidden.
    /// </summary>
    public static string HideStructuralFacilitated(IRobustRandom random, string sequence)
    {
        var bases = sequence.ToCharArray();
        HidePositions(random, bases, random.Next(3, 5));
        return new string(bases);
    }

    private static void HidePositions(IRobustRandom random, char[] bases, int remaining)
    {
        var pairs = bases.Length / 2;
        var pairChance = (float) remaining / bases.Length;
        while (remaining > 0)
        {
            var pair = random.Next(pairs);
            if (remaining >= 2 && random.Prob(pairChance))
            {
                Hide(pair);
                Hide(pair + pairs);
            }
            else
                Hide(pair + (random.Prob(0.5f) ? pairs : 0));
        }
        void Hide(int index)
        {
            if (bases[index] == 'X') return;
            bases[index] = 'X';
            remaining--;
        }
    }

    public static bool Validate(string original, string answer, string expected)
    {
        if (original.Length != expected.Length || answer.Length != expected.Length)
            return false;
        for (var i = 0; i < answer.Length; i++)
            if (answer[i] != expected[i] || (original[i] != 'X' && original[i] != answer[i]))
                return false;
        return true;
    }
}

[Prototype("geneticRecipe")]
public sealed partial class GeneticRecipePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;
    [DataField(required: true)] public HashSet<string> Required = new();
    [DataField(required: true)] public string Result = string.Empty;
}

[Serializable, NetSerializable]
public sealed class GeneticGeneState
{
    public int Number;
    public string Name = "???";
    public bool Discovered;
    public bool Active;
}

[Serializable, NetSerializable]
public sealed class GeneticPuzzleState
{
    public int Token;
    public string Title = string.Empty;
    public string Original = string.Empty;
    public bool Appearance;
}

[Serializable, NetSerializable]
public sealed class GeneticSelectMessage(int number) : BoundUserInterfaceMessage
{
    public readonly int Number = number;
}

[Serializable, NetSerializable]
public sealed class GeneticToggleMessage(int number) : BoundUserInterfaceMessage
{
    public readonly int Number = number;
}

[Serializable, NetSerializable]
public sealed class GeneticSubmitMessage(int token, string answer) : BoundUserInterfaceMessage
{
    public readonly int Token = token;
    public readonly string Answer = answer;
}

[Serializable, NetSerializable]
public sealed class GeneticAppearanceMessage(string field, string value) : BoundUserInterfaceMessage
{
    public readonly string Field = field;
    public readonly string Value = value;
}

[Serializable, NetSerializable]
public sealed class GeneticBufferAppearanceMessage(int buffer, string field) : BoundUserInterfaceMessage
{
    public readonly int Buffer = buffer;
    public readonly string Field = field;
}

[Serializable, NetSerializable]
public sealed class GeneticCombineMessage(int first, int second) : BoundUserInterfaceMessage
{
    public readonly int First = first;
    public readonly int Second = second;
}
