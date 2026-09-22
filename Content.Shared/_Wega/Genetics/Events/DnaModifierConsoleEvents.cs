using Content.Shared.Chemistry.Reagent;
using Content.Shared.Genetics.UI;
using Robust.Shared.Serialization;

namespace Content.Shared.Genetics;

[Serializable, NetSerializable]
public sealed class DnaModifierUpdateEvent : BoundUserInterfaceMessage
{
    public NetEntity Uid { get; }

    public DnaModifierUpdateEvent(NetEntity uid)
    {
        Uid = uid;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierConsoleEjectEvent : BoundUserInterfaceMessage
{
    public NetEntity Uid { get; }

    public DnaModifierConsoleEjectEvent(NetEntity uid)
    {
        Uid = uid;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierConsoleEjectRejuveEvent : BoundUserInterfaceMessage
{
    public NetEntity Uid { get; }

    public DnaModifierConsoleEjectRejuveEvent(NetEntity uid)
    {
        Uid = uid;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierConsoleReagentButtonEvent : BoundUserInterfaceMessage
{
    public NetEntity Uid { get; }
    public DnaModifierReagentAmount Amount { get; }
    public ReagentId ReagentId { get; }

    public DnaModifierConsoleReagentButtonEvent(NetEntity uid, DnaModifierReagentAmount amount, ReagentId reagentId)
    {
        Uid = uid;
        Amount = amount;
        ReagentId = reagentId;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierConsoleSaveServerEvent : BoundUserInterfaceMessage
{
    public NetEntity Uid { get; }
    public int CurrentSection { get; }
    public int CurrentType { get; }

    public DnaModifierConsoleSaveServerEvent(NetEntity uid, int currentSection, int currentType)
    {
        Uid = uid;
        CurrentSection = currentSection;
        CurrentType = currentType;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierConsoleInjectorEvent : BoundUserInterfaceMessage
{
    public NetEntity Uid { get; }
    public int Index { get; }

    public DnaModifierConsoleInjectorEvent(NetEntity uid, int index)
    {
        Uid = uid;
        Index = index;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierInjectBlockEvent : BoundUserInterfaceMessage
{
    public NetEntity Uid { get; }
    public int Index { get; }
    public int CurrentBlock { get; }

    public DnaModifierInjectBlockEvent(NetEntity uid, int index, int currentBlock)
    {
        Uid = uid;
        Index = index;
        CurrentBlock = currentBlock;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierConsoleSubjectInjectEvent : BoundUserInterfaceMessage
{
    public NetEntity Uid { get; }
    public int Index { get; }

    public DnaModifierConsoleSubjectInjectEvent(NetEntity uid, int index)
    {
        Uid = uid;
        Index = index;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierConsoleClearBufferEvent : BoundUserInterfaceMessage
{
    public NetEntity Uid { get; }
    public int Index { get; }

    public DnaModifierConsoleClearBufferEvent(NetEntity uid, int index)
    {
        Uid = uid;
        Index = index;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierConsoleRenameBufferEvent : BoundUserInterfaceMessage
{
    public NetEntity Console { get; }
    public NetEntity User { get; }
    public int Index { get; }

    public DnaModifierConsoleRenameBufferEvent(NetEntity console, NetEntity user, int index)
    {
        Console = console;
        User = user;
        Index = index;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierConsoleExportOnDiskEvent : BoundUserInterfaceMessage
{
    public NetEntity Uid { get; }
    public int Index { get; }

    public DnaModifierConsoleExportOnDiskEvent(NetEntity uid, int index)
    {
        Uid = uid;
        Index = index;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierConsoleExportFromDiskEvent : BoundUserInterfaceMessage
{
    public NetEntity Uid { get; }
    public int Index { get; }

    public DnaModifierConsoleExportFromDiskEvent(NetEntity uid, int index)
    {
        Uid = uid;
        Index = index;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierConsoleClearDiskEvent : BoundUserInterfaceMessage
{
    public NetEntity Uid { get; }

    public DnaModifierConsoleClearDiskEvent(NetEntity uid)
    {
        Uid = uid;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierConsoleReleverationEvent : BoundUserInterfaceMessage
{
    public NetEntity Uid { get; }
    public int CurrentTab { get; }
    public string CurrentBlock { get; }
    public int CurrentValue { get; }
    public float Intensity { get; }

    public DnaModifierConsoleReleverationEvent(NetEntity uid, int currentTab, string currentBlock, int currentValue, float intensity)
    {
        Uid = uid;
        CurrentBlock = currentBlock;
        CurrentValue = currentValue;
        CurrentTab = currentTab;
        Intensity = intensity;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierConsoleReleverationsEvent : BoundUserInterfaceMessage
{
    public NetEntity Uid { get; }
    public int CurrentTab { get; }
    public float Intensity { get; }
    public float Duration { get; }

    public DnaModifierConsoleReleverationsEvent(NetEntity uid, int currentTab, float intensity, float duration)
    {
        Uid = uid;
        CurrentTab = currentTab;
        Intensity = intensity;
        Duration = duration;
    }
}

public sealed class CureDnaDiseaseAttemptEvent : BoundUserInterfaceMessage
{
    public float CureChance { get; }

    public CureDnaDiseaseAttemptEvent(float cureChance)
    {
        CureChance = cureChance;
    }
}

public sealed class MutateDnaAttemptEvent : BoundUserInterfaceMessage
{
    public MutateDnaAttemptEvent()
    {
    }
}
