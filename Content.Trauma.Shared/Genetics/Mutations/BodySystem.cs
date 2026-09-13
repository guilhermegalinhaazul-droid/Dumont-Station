// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.GameObjects;

namespace Content.Trauma.Shared.Genetics.Mutations;

/// <summary>
/// Local compatibility shim for imported Trauma genetics files that refer to a
/// BodySystem API surface without depending on the full Content.Trauma.Common
/// stack. The host integration keeps the Wega path and limits the shim to the
/// methods the sequencing and mutation files actually touch.
/// </summary>
public sealed class BodySystem : EntitySystem
{
    public IEnumerable<Entity<TComponent>> GetOrgans<TComponent>(EntityUid mob)
        where TComponent : IComponent
        => Array.Empty<Entity<TComponent>>();

    public IEnumerable<Entity<TComponent>> GetBodyChildren<TComponent>(EntityUid mob)
        where TComponent : IComponent
        => Array.Empty<Entity<TComponent>>();
}
