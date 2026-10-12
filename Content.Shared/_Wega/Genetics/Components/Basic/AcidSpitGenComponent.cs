using Robust.Shared.Prototypes;

namespace Content.Shared.Genetics;

[RegisterComponent]
public sealed partial class AcidSpitGenComponent : Component
{
    [DataField]
    public EntProtoId ActionId = "ActionGenAcidSpit";

    public EntityUid? ActionEntity;

    // Keep acid ammunition isolated from the laser eyes ammunition.
    public EntityUid? GunEntity;
}
