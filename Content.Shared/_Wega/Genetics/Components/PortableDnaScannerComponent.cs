using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PortableDnaScannerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? ScannedEntity;

    [ViewVariables]
    public EnzymeInfo? Sample;

    [ViewVariables]
    public string SampleDna = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PortableDnaScannerState : BoundUserInterfaceState
{
    public string SubjectName = string.Empty;
    public string SubjectDna = string.Empty;
    public EnzymeInfo? Sample;
    public bool HasDisk;

    public PortableDnaScannerState(string subjectName, string subjectDna, EnzymeInfo? sample, bool hasDisk)
    {
        SubjectName = subjectName;
        SubjectDna = subjectDna;
        Sample = sample;
        HasDisk = hasDisk;
    }
}

[Serializable, NetSerializable]
public sealed class PortableDnaScannerClearMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class PortableDnaScannerLoadMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class PortableDnaScannerSaveMessage(PortableDnaSampleKind kind) : BoundUserInterfaceMessage
{
    public readonly PortableDnaSampleKind Kind = kind;
}

[Serializable, NetSerializable]
public enum PortableDnaSampleKind : byte
{
    Unique,
    Structural,
    Both
}

[Serializable, NetSerializable]
public enum PortableDnaScannerUiKey : byte
{
    Key
}
