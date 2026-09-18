// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;
using Content.Trauma.Shared.Genetics.Mutations;

namespace Content.Trauma.Shared.Genetics.Abilities;

/// <summary>
/// Handles running entity effects when a mutation is added or removed.
/// Adapted to Dumont's current SharedEntityEffectSystem API.
/// </summary>
public sealed partial class EffectsMutationSystem : EntitySystem
{
    [Dependency] private readonly SharedEntityEffectSystem _effects = default!;

    [SubscribeLocalEvent]
    private void OnAdded(Entity<EffectsMutationComponent> ent, ref MutationAddedEvent args)
    {
        if (args.Automatic && ent.Comp.IgnoreAutomatic)
            return;

        Apply(args.Target, ent.Comp.Added);
    }

    [SubscribeLocalEvent]
    private void OnRemoved(Entity<EffectsMutationComponent> ent, ref MutationRemovedEvent args)
    {
        if (args.Automatic && ent.Comp.IgnoreAutomatic)
            return;

        Apply(args.Target, ent.Comp.Removed);
    }

    private void Apply(EntityUid target, EntityEffect[] effects)
    {
        var effectArgs = new EntityEffectBaseArgs(target, EntityManager);
        foreach (var effect in effects)
        {
            if (effect.ShouldApply(effectArgs))
                _effects.Effect(effect, effectArgs);
        }
    }
}
