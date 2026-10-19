using Robust.Shared.GameStates;

namespace Content.Shared.Genetics;

[RegisterComponent, NetworkedComponent]
public sealed partial class PortableGeneticAnalyzerComponent : Component
{
    [ViewVariables]
    public EntityUid? Target;
}
