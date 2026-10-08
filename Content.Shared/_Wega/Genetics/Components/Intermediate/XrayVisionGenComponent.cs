using Robust.Shared.GameStates;

namespace Content.Shared.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XrayVisionGenComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Range = 12f;
}
