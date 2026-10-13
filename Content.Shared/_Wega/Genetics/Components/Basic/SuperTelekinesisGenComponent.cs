using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Genetics;

/// <summary>
/// Grants a second, left-side hand whose tether can move living entities.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SuperTelekinesisGenComponent : Component
{
    [ValidatePrototypeId<EntityPrototype>, DataField("itemPrototype"), ViewVariables(VVAccess.ReadWrite)]
    public string ItemPrototype = "HandSuperTelekinesisGun";

    [DataField("handId"), ViewVariables(VVAccess.ReadWrite)]
    public string HandId = "super-telekinesis-hand";

    [ViewVariables]
    public EntityUid? TelekinesisItem;
}

/// <summary>
/// Marks the super-telekinesis item so its tether is restricted to mob entities.
/// </summary>
[RegisterComponent]
public sealed partial class SuperTelekinesisTargetingComponent : Component
{
}
