// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared.Genetics;

[Serializable, NetSerializable]
public enum EiAppearanceRegion : byte
{
    HairColor,
    SecondaryHairColor,
    BeardColor,
    SkinTone,
    FurColor,
    HeadAccessoryColor,
    HeadMarkingColor,
    BodyMarkingColor,
    TailMarkingColor,
    EyeColor,
    Gender,
    BeardStyle,
    HairStyle,
    HeadAccessoryStyle,
    HeadMarkingStyle,
    BodyMarkingStyle,
    TailMarkingStyle,
}

[Serializable, NetSerializable]
public sealed class EiAppearanceRegionOption
{
    public EiAppearanceRegion Region { get; set; }
    public string Name { get; set; } = string.Empty;
}

[Serializable, NetSerializable]
public sealed class EiBasePairState
{
    public int Index { get; set; }
    public string Top { get; set; } = "A";
    public string Bottom { get; set; } = "T";
    public int HydrogenBonds { get; set; }
    public bool Changed { get; set; }
    public bool Valid { get; set; }
}

[Serializable, NetSerializable]
public sealed class DnaModifierEiEditRequestEvent : EntityEventArgs
{
    public NetEntity Console { get; }
    public int BufferIndex { get; }
    public EiAppearanceRegion Region { get; }

    public DnaModifierEiEditRequestEvent(NetEntity console, int bufferIndex, EiAppearanceRegion region)
    {
        Console = console;
        BufferIndex = bufferIndex;
        Region = region;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierEiSetBaseEvent : EntityEventArgs
{
    public NetEntity Console { get; }
    public int PairIndex { get; }
    public bool BottomStrand { get; }
    public string Base { get; }

    public DnaModifierEiSetBaseEvent(NetEntity console, int pairIndex, bool bottomStrand, string @base)
    {
        Console = console;
        PairIndex = pairIndex;
        BottomStrand = bottomStrand;
        Base = @base;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierEiResetEditEvent : EntityEventArgs
{
    public NetEntity Console { get; }

    public DnaModifierEiResetEditEvent(NetEntity console)
    {
        Console = console;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierEiCommitEditEvent : EntityEventArgs
{
    public NetEntity Console { get; }

    public DnaModifierEiCommitEditEvent(NetEntity console)
    {
        Console = console;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierEiEditingStateEvent : EntityEventArgs
{
    public NetEntity Console { get; }
    public int BufferIndex { get; }
    public string ProfileName { get; }
    public EiAppearanceRegion Region { get; }
    public List<EiAppearanceRegionOption> Regions { get; }
    public List<EiBasePairState> Pairs { get; }
    public int BondCost { get; }
    public int BondBudget { get; }
    public bool CanCommit { get; }
    public string? Feedback { get; }

    public DnaModifierEiEditingStateEvent(
        NetEntity console,
        int bufferIndex,
        string profileName,
        EiAppearanceRegion region,
        List<EiAppearanceRegionOption> regions,
        List<EiBasePairState> pairs,
        int bondCost,
        int bondBudget,
        bool canCommit,
        string? feedback)
    {
        Console = console;
        BufferIndex = bufferIndex;
        ProfileName = profileName;
        Region = region;
        Regions = regions;
        Pairs = pairs;
        BondCost = bondCost;
        BondBudget = bondBudget;
        CanCommit = canCommit;
        Feedback = feedback;
    }
}
