// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Events;
using Content.Shared.Damage;
using Content.Shared.Flash;
using Content.Shared.Mobs;
using Content.Shared.Speech;
using Content.Shared.Weapons.Melee.Events;
using Content.Trauma.Shared.Genetics.Mutations;

namespace Content.Trauma.Shared.Genetics.Mutations;

/// <summary>
/// Relays host events that are available in Dumont from a mutated mob to its mutation entities.
/// This replaces the imported relay file whose optional Trauma-only event namespaces are not
/// part of Dumont's main content assembly.
/// </summary>
public sealed class MutationRelaySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutatableComponent, AfterFlashedEvent>(RelayEvent);
        SubscribeLocalEvent<MutatableComponent, MobStateChangedEvent>(RelayEvent);
        SubscribeLocalEvent<MutatableComponent, BleedModifierEvent>(RelayEvent);
        SubscribeLocalEvent<MutatableComponent, DamageModifyEvent>(RelayEvent);
        SubscribeLocalEvent<MutatableComponent, GetUserMeleeDamageEvent>(RelayEvent);
        SubscribeLocalEvent<MutatableComponent, AccentGetEvent>(RelayEvent);
    }

    private void RelayEvent<T>(Entity<MutatableComponent> ent, ref T args) where T : notnull
    {
        foreach (var uid in ent.Comp.Mutations.Values)
            RaiseLocalEvent(uid, ref args);
    }
}
