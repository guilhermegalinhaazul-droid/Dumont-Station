// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;

namespace Content.Trauma.Shared.Actions;

/// <summary>
/// Minimal compatibility component for the imported trauma genetics action projectile
/// flow. It carries the mutation/action container reference that the chem spike
/// transfer pathway expects to see in the object graph.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(EntitySystem))]
public sealed partial class ActionProjectileComponent : Component
{
    [DataField]
    public EntityUid? Container;
}
