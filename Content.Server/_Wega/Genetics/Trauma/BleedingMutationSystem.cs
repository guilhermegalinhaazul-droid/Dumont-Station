// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Trauma.Shared.Genetics.Mutations;

namespace Content.Trauma.Shared.Genetics.Abilities;

/// <summary>
/// Dumont-compatible Trauma bleeding mutation.
/// Bleed-rate modification is relayed through BleedModifierEvent; blood regeneration
/// is adjusted directly on the current BloodstreamComponent.
/// </summary>
public sealed partial class BleedingMutationSystem : EntitySystem
{
    [Dependency] private readonly EntityQuery<BloodstreamComponent> _bloodstreamQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BleedingMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<BleedingMutationComponent, MutationRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<BleedingMutationComponent, BleedModifierEvent>(OnBleedModifier);
    }

    private void OnAdded(Entity<BleedingMutationComponent> ent, ref MutationAddedEvent args)
    {
        if (!_bloodstreamQuery.TryComp(args.Target, out var blood))
            return;

        blood.BloodRefreshAmount *= ent.Comp.RefreshModifier;
        DirtyField(args.Target, blood, nameof(BloodstreamComponent.BloodRefreshAmount));
    }

    private void OnRemoved(Entity<BleedingMutationComponent> ent, ref MutationRemovedEvent args)
    {
        if (!_bloodstreamQuery.TryComp(args.Target, out var blood) || ent.Comp.RefreshModifier == 0f)
            return;

        blood.BloodRefreshAmount /= ent.Comp.RefreshModifier;
        DirtyField(args.Target, blood, nameof(BloodstreamComponent.BloodRefreshAmount));
    }

    private void OnBleedModifier(Entity<BleedingMutationComponent> ent, ref BleedModifierEvent args)
    {
        args.BleedAmount *= ent.Comp.BleedModifier;
    }
}
