// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Trauma.Shared.Genetics.Mutations;

namespace Content.Trauma.Shared.Genetics.Abilities;

/// <summary>
/// Dumont server adapter for Trauma's metabolism-speed mutation.
/// MetabolizerComponent is server-side in the current codebase.
/// </summary>
public sealed partial class MetabolismSpeedMutationSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly EntityQuery<MetabolizerComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MetabolismSpeedMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<MetabolismSpeedMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<MetabolismSpeedMutationComponent> ent, ref MutationAddedEvent args)
        => Modify(args.Target, ent.Comp.Bonus);

    private void OnRemoved(Entity<MetabolismSpeedMutationComponent> ent, ref MutationRemovedEvent args)
        => Modify(args.Target, -ent.Comp.Bonus);

    private void Modify(EntityUid uid, float add)
    {
        if (_query.TryComp(uid, out var mobComp))
        {
            mobComp.UpdateIntervalMultiplier += add;
            Dirty(uid, mobComp);
        }

        foreach (var organ in _body.GetBodyOrgans(uid))
        {
            if (!_query.TryComp(organ.Id, out var organComp))
                continue;

            organComp.UpdateIntervalMultiplier += add;
            Dirty(organ.Id, organComp);
        }
    }
}
