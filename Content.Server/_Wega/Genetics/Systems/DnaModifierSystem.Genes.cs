// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Linq;
using Content.Shared.Genetics;
using Content.Shared.Genetics.Systems;

namespace Content.Server.Genetics.System;

public sealed partial class DnaModifierSystem
{
    public void ClearAppliedGenes(Entity<DnaModifierComponent> subject)
    {
        if (subject.Comp.EnzymesPrototypes == null)
            return;

        foreach (var gene in subject.Comp.EnzymesPrototypes)
        {
            if (gene.EnzymesPrototypeId != StructuralEnzymesIndexerSystem.SpeciesGene)
                gene.Active = false;
        }

        TryChangeStructuralEnzymes(subject);
        Dirty(subject);
    }

    public int ActivateAllGenes(Entity<DnaModifierComponent> subject)
    {
        if (subject.Comp.EnzymesPrototypes == null)
            return 0;

        var activated = 0;
        foreach (var gene in subject.Comp.EnzymesPrototypes)
        {
            if (gene.EnzymesPrototypeId == StructuralEnzymesIndexerSystem.SpeciesGene || gene.Active)
                continue;

            gene.Active = true;
            activated++;
        }

        TryChangeStructuralEnzymes(subject);
        Dirty(subject);
        return activated;
    }

    public bool IsGeneActive(Entity<DnaModifierComponent> subject, EnzymesPrototypeInfo gene)
    {
        if (gene.EnzymesPrototypeId == StructuralEnzymesIndexerSystem.SpeciesGene)
            return gene.Active;
        return subject.Comp.AppliedGenes.Contains(gene.EnzymesPrototypeId);

    }

    public void SetGeneActive(Entity<DnaModifierComponent> subject, EnzymesPrototypeInfo gene, bool active)
    {
        if (active && _prototype.TryIndex<StructuralEnzymesPrototype>(gene.EnzymesPrototypeId, out var prototype))
        {
            var genes = subject.Comp.EnzymesPrototypes!;
            if (genes.Any(g => prototype.Conflicts.Contains(g.EnzymesPrototypeId) && IsGeneActive(subject, g)) ||
                prototype.Required.Any(id => !genes.Any(g => g.EnzymesPrototypeId == id && IsGeneActive(subject, g))))
                return;
            foreach (var removed in genes.Where(g => prototype.Removes.Contains(g.EnzymesPrototypeId)))
                removed.Active = false;
        }
        gene.Active = active;
        TryChangeStructuralEnzymes(subject);
        Dirty(subject);
    }
}
