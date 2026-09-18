// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Radio.Components;
using Content.Trauma.Shared.Genetics.Abilities;
using Content.Trauma.Shared.Genetics.Mutations;

namespace Content.Server.Genetics.System;

/// <summary>
/// Server-side adapter for Trauma's intrinsic radio mutation.
/// The radio receiver/transmitter components live in Content.Server in Dumont,
/// so this behavior cannot be compiled into Content.Shared.
/// </summary>
public sealed class HybridRadioMutationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<RadioMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<RadioMutationComponent> ent, ref MutationAddedEvent args)
    {
        var mob = args.Target.Owner;
        EnsureComp<IntrinsicRadioReceiverComponent>(mob);
        var active = EnsureComp<ActiveRadioComponent>(mob);
        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(mob);

        foreach (var channel in ent.Comp.Channels)
        {
            var channelId = channel.ToString();
            if (active.Channels.Add(channelId))
                ent.Comp.AddedActive.Add(channel);
            if (transmitter.Channels.Add(channelId))
                ent.Comp.AddedTransmitters.Add(channel);
        }

        Dirty(ent);
    }

    private void OnRemoved(Entity<RadioMutationComponent> ent, ref MutationRemovedEvent args)
    {
        var mob = args.Target.Owner;
        if (!TryComp<ActiveRadioComponent>(mob, out var active)
            || !TryComp<IntrinsicRadioTransmitterComponent>(mob, out var transmitter))
        {
            return;
        }

        foreach (var channel in ent.Comp.AddedActive)
            active.Channels.Remove(channel.ToString());

        foreach (var channel in ent.Comp.AddedTransmitters)
            transmitter.Channels.Remove(channel.ToString());

        ent.Comp.AddedActive.Clear();
        ent.Comp.AddedTransmitters.Clear();
        Dirty(ent);

        if (active.Channels.Count == 0)
        {
            RemComp<ActiveRadioComponent>(mob);
            RemComp<IntrinsicRadioReceiverComponent>(mob);
        }

        if (transmitter.Channels.Count == 0)
            RemComp<IntrinsicRadioTransmitterComponent>(mob);
    }
}
