// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared.Genetics;

/// <summary>
///     Marks an entity whose head layers should be rendered at an enlarged size.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MegacephalyComponent : Component
{
}
