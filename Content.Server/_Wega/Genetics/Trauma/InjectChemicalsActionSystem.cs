// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Popups;
using Content.Trauma.Shared.Genetics.Mutations;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Trauma.Shared.Genetics.Abilities;

/// <summary>
/// Dumont server adapter for Trauma's chemical-injection mutation action.
/// </summary>
public sealed partial class InjectChemicalsActionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MutationSystem _mutation = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InjectChemicalsActionComponent, InjectChemicalsActionEvent>(OnAction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<InjectChemicalsActionComponent>();
        var now = _timing.CurTime;
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextComedown is not { } next || now < next)
                continue;

            Comedown((uid, comp));
        }
    }

    private void OnAction(Entity<InjectChemicalsActionComponent> ent, ref InjectChemicalsActionEvent args)
    {
        args.Handled = true;
        InjectMain(ent, args.Performer);
    }

    private void InjectMain(Entity<InjectChemicalsActionComponent> ent, EntityUid target)
    {
        _popup.PopupEntity(Loc.GetString(ent.Comp.Main.Popup), target, target);
        ent.Comp.NextComedown = _timing.CurTime + ent.Comp.ComedownDelay;
        Inject(target, ent.Comp.Main.Reagents, ent.Comp.Main.Quantity);
    }

    private void Comedown(Entity<InjectChemicalsActionComponent> ent)
    {
        if (_mutation.GetActionMutation(ent)?.Comp?.Target is not { } target)
            return;

        _popup.PopupEntity(Loc.GetString(ent.Comp.Comedown.Popup), target, target);
        ent.Comp.NextComedown = null;
        Inject(target, ent.Comp.Comedown.Reagents, ent.Comp.Comedown.Quantity);
    }

    private void Inject(EntityUid target, List<ProtoId<ReagentPrototype>> reagents, FixedPoint2 quantity)
    {
        foreach (var reagent in reagents)
        {
            var solution = new Solution(reagent, quantity);
            _bloodstream.TryAddToChemicals(target, solution);
        }
    }
}
