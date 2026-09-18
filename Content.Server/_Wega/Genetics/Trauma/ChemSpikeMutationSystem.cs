// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Systems;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Trauma.Shared.Actions;
using Content.Trauma.Shared.Genetics.Mutations;

namespace Content.Trauma.Shared.Genetics.Abilities;

/// <summary>
/// Dumont server adapter for Trauma chem-spike behavior.
/// </summary>
public sealed partial class ChemSpikeMutationSystem : EntitySystem
{
    [Dependency] private readonly MutationSystem _mutation = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedProjectileSystem _projectile = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChemSpikeMutationComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ChemTransferActionComponent, ChemTransferActionEvent>(OnTransfer);
        SubscribeLocalEvent<ChemTransferProjectileComponent, EmbedEvent>(OnProjectileEmbed);
        SubscribeLocalEvent<ChemTransferProjectileComponent, EmbedDetachEvent>(OnProjectileDetach);
        SubscribeLocalEvent<ChemTransferProjectileComponent, ComponentShutdown>(OnProjectileShutdown);
    }

    private void OnShutdown(Entity<ChemSpikeMutationComponent> ent, ref ComponentShutdown args)
    {
        PredictedQueueDel(ent.Comp.ActionEntity);
    }

    private void OnTransfer(Entity<ChemTransferActionComponent> ent, ref ChemTransferActionEvent args)
    {
        if (args.Action.Comp.Container is not { } mutation
            || !TryComp<ChemSpikeMutationComponent>(mutation, out var comp)
            || comp.Target is not { } target)
        {
            return;
        }

        if (TryComp<BloodstreamComponent>(args.Performer, out var sourceBlood)
            && _solutions.ResolveSolution(
                args.Performer,
                sourceBlood.ChemicalSolutionName,
                ref sourceBlood.ChemicalSolution,
                out var sourceSolution))
        {
            var quantity = comp.MaxQuantity < sourceSolution.Volume
                ? comp.MaxQuantity
                : sourceSolution.Volume;

            if (quantity > 0)
            {
                var removed = _solutions.SplitSolution(sourceBlood.ChemicalSolution!.Value, quantity);
                _blood.TryAddToChemicals(target, removed);
            }
        }

        args.Handled = true;

        if (comp.Projectile is { } projectile)
            _projectile.EmbedDetach(projectile, null);

        SetMutationTarget((mutation, comp), null, null);
    }

    private void OnProjectileEmbed(Entity<ChemTransferProjectileComponent> ent, ref EmbedEvent args)
    {
        if (HasComp<BloodstreamComponent>(args.Embedded))
            SetProjectileTarget(ent, args.Embedded);
    }

    private void OnProjectileDetach(Entity<ChemTransferProjectileComponent> ent, ref EmbedDetachEvent args)
        => SetProjectileTarget(ent, null);

    private void OnProjectileShutdown(Entity<ChemTransferProjectileComponent> ent, ref ComponentShutdown args)
        => SetProjectileTarget(ent, null);

    private void SetProjectileTarget(EntityUid uid, EntityUid? target)
    {
        if (CompOrNull<ActionProjectileComponent>(uid)?.Container is not { } mutation)
            return;

        SetMutationTarget(mutation, target, uid);
    }

    private void SetMutationTarget(Entity<ChemSpikeMutationComponent?> ent, EntityUid? target, EntityUid? proj = null)
    {
        if (!Resolve(ent, ref ent.Comp)
            || (ent.Comp.Target == target && ent.Comp.Projectile == proj)
            || _mutation.GetMutationTarget(ent.Owner) is not { } user)
        {
            return;
        }

        ent.Comp.Target = target;
        ent.Comp.Projectile = proj;

        if (target != null)
            _actions.AddAction(user, ref ent.Comp.ActionEntity, ent.Comp.Action, container: ent.Owner);
        else
            _actions.RemoveAction(ent.Comp.ActionEntity);

        var key = target != null ? "set" : "reset";
        _popup.PopupEntity(Loc.GetString("MutationChemSpike-target-" + key), user, user);
    }
}
