using Robust.Shared.Prototypes;

namespace Content.Shared.Genetics;

[RegisterComponent]
public sealed partial class LaserEyesGenComponent : Component
{
    [DataField]
    public EntProtoId ActionId = "ActionGenLaserEyes";

    public EntityUid? ActionEntity;

    // The ability owns a private gun entity so it cannot share ammo providers
    // with another genetic weapon installed on the same character.
    public EntityUid? GunEntity;
}
