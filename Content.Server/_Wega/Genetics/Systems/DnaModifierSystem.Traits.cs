// SPDX-License-Identifier: AGPL-3.0-or-later
// Passive modifiers adapted from Trauma's mutation ability systems.
using System.Linq;
using System.Numerics;
using Content.Server.Body.Components;
using Content.Server.Temperature.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Genetics;
using Content.Shared.Humanoid;
using Content.Shared.Sprite;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Genetics.System;

public sealed partial class DnaModifierSystem
{
    [Dependency] private readonly SharedActionsSystem _geneActions = default!;
    [Dependency] private readonly SharedBodySystem _geneBody = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _geneScale = default!;

    private void InitializeGeneTraits()
    {
        SubscribeLocalEvent<DnaModifierComponent, GetUserMeleeDamageEvent>(OnGeneMeleeDamage);
        SubscribeLocalEvent<DnaModifierComponent, DamageModifyEvent>(OnGeneDamage);
    }

    private void OnGeneMeleeDamage(Entity<DnaModifierComponent> ent, ref GetUserMeleeDamageEvent args)
    {
        foreach (var id in ent.Comp.AppliedGenes)
            args.Damage *= _prototype.Index<StructuralEnzymesPrototype>(id).MeleeMultiplier;
    }

    private void OnGeneDamage(Entity<DnaModifierComponent> ent, ref DamageModifyEvent args)
    {
        foreach (var id in ent.Comp.AppliedGenes)
        {
            if (_prototype.Index<StructuralEnzymesPrototype>(id).DamageModifiers is { } modifiers)
                args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifiers);
        }
    }

    private void ApplyGeneTraits(Entity<DnaModifierComponent> subject, StructuralEnzymesPrototype gene, bool active)
    {
        var direction = active ? 1f : -1f;
        float Factor(float factor) => active ? factor : 1f / factor;
        if (gene.MetabolismBonus != 0)
        {
            if (TryComp<MetabolizerComponent>(subject, out var metabolism))
                metabolism.UpdateIntervalMultiplier += direction * gene.MetabolismBonus;
            foreach (var (organ, _) in _geneBody.GetBodyOrgans(subject))
                if (TryComp<MetabolizerComponent>(organ, out var organMetabolism))
                    organMetabolism.UpdateIntervalMultiplier += direction * gene.MetabolismBonus;
        }
        if (gene.BloodRefreshMultiplier != 1 && TryComp<BloodstreamComponent>(subject, out var blood))
            blood.BloodRefreshAmount *= Factor(gene.BloodRefreshMultiplier);
        if (gene.Scale != Vector2.One)
            _geneScale.SetSpriteScale(subject, _geneScale.GetSpriteScale(subject) * new Vector2(Factor(gene.Scale.X), Factor(gene.Scale.Y)));
        if (TryComp<TemperatureComponent>(subject, out var temperature))
        {
            temperature.ColdDamageThreshold += direction * gene.ColdOffset;
            temperature.HeatDamageThreshold += direction * gene.HeatOffset;
        }
        if (TryComp<ThermalRegulatorComponent>(subject, out var regulator))
        {
            regulator.ShiveringHeatRegulation *= Factor(gene.Shivering);
            regulator.SweatHeatRegulation *= Factor(gene.Sweating);
            regulator.MetabolismHeat *= Factor(gene.MetabolismHeat);
            regulator.ImplicitHeatRegulation *= Factor(gene.HeatRegulation);
        }
        if (active)
        {
            var actions = new List<EntityUid>();
            foreach (var prototype in gene.Actions)
            {
                EntityUid? action = null;
                _geneActions.AddAction(subject, ref action, prototype);
                if (action is { } uid)
                    actions.Add(uid);
            }
            subject.Comp.GeneActions[gene.ID] = actions;
        }
        else if (subject.Comp.GeneActions.Remove(gene.ID, out var actions))
        {
            foreach (var action in actions)
            {
                if (TryComp<ActionComponent>(action, out var actionComponent))
                    _geneActions.RemoveAction((action, actionComponent));
            }
        }
    }
}
