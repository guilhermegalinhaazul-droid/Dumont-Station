using Robust.Shared.GameStates;

namespace Content.Shared.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class XrayVisionGenComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Range = 12f;
}
