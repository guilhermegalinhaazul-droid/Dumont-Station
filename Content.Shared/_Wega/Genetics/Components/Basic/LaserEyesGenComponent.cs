using Robust.Shared.Prototypes;

namespace Content.Shared.Genetics;

[RegisterComponent]
public sealed partial class LaserEyesGenComponent : Component
{
    [DataField]
    public EntProtoId ActionId = "ActionGenLaserEyes";

    public EntityUid? ActionEntity;
}
