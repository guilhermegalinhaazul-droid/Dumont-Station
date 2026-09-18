// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Forensics.Components;
using Content.Shared.Genetics;
using Content.Shared.Humanoid;
using Content.Trauma.Shared.Genetics.Mutations;
using Robust.Shared.Prototypes;

namespace Content.Server.Genetics.System;

public sealed partial class DnaModifierSystem
{
    [Dependency] private readonly MutationSystem _eiMutation = default!;
    [Dependency] private readonly ScannedGenomeSystem _eiGenome = default!;

    /// <summary>
    /// Captures the genetic/hereditary state owned by an organism into Wega's existing
    /// EnzymeInfo storage model. No EntityUid is retained, so the EI survives donor deletion.
    /// </summary>
    public EnzymeInfo CaptureGeneticProfile(Entity<DnaModifierComponent> source)
    {
        var profile = new EnzymeInfo
        {
            IsFullGeneticProfile = true,
            GeneticIdentityName = Name(source.Owner),
            Identifier = CloneUniqueIdentifiers(source.Comp.UniqueIdentifiers),
            Info = CloneEnzymesPrototypes(source.Comp.EnzymesPrototypes),
        };

        if (TryComp<DnaComponent>(source.Owner, out var dna))
            profile.Dna = dna.DNA;

        if (TryComp<HumanoidAppearanceComponent>(source.Owner, out var humanoid))
            profile.SpeciesId = humanoid.Species.ToString();

        if (_eiMutation.IsMutatable(source.Owner))
        {
            profile.HasTraumaMutationState = true;
            var mutationData = _eiMutation.GetMutatableData(source.Owner);
            profile.TraumaDormantMutations = mutationData.Dormant
                .Select(id => id.ToString())
                .ToList();
            profile.TraumaActiveMutations = mutationData.Mutations
                .Select(id => id.ToString())
                .ToList();
        }

        return profile;
    }

    /// <summary>
    /// Applies a stored EI to the receiver. The profile is server-owned data from a Wega
    /// buffer/disk; the client only chooses the buffer index.
    /// </summary>
    public bool ApplyGeneticProfile(Entity<DnaModifierComponent> target, EnzymeInfo profile)
    {
        if (!profile.IsFullGeneticProfile)
            return false;

        var hasTraumaState = profile.HasTraumaMutationState;

        List<EntProtoId<MutationComponent>> active = new();
        List<EntProtoId<MutationComponent>> dormant = new();

        if (_eiMutation.IsMutatable(target.Owner) && hasTraumaState)
        {
            // A dead body cannot safely reconstruct active Trauma mutations through AddMutation.
            if (_eiMutation.GetMutatable(target.Owner, force: false) == null)
                return false;

            if (!TryResolveMutationIds(profile.TraumaActiveMutations, active)
                || !TryResolveMutationIds(profile.TraumaDormantMutations, dormant)
                || !ValidateMutationSet(active))
            {
                return false;
            }
        }

        // Deep-copy Wega state so later edits to the receiver cannot mutate the stored EI.
        if (profile.Identifier != null)
            target.Comp.UniqueIdentifiers = CloneUniqueIdentifiers(profile.Identifier);

        if (profile.Info != null)
        {
            var receiverFormBlock = target.Comp.EnzymesPrototypes?
                .FirstOrDefault(enzyme => enzyme.Order == 55);
            var appliedInfo = CloneEnzymesPrototypes(profile.Info) ?? new List<EnzymesPrototypeInfo>();

            // Order 55 controls Wega form changes and can replace/delete the receiver entity.
            // EI transports portable genetic traits; it does not silently transform species.
            appliedInfo.RemoveAll(enzyme => enzyme.Order == 55);
            if (receiverFormBlock != null)
                appliedInfo.Add((EnzymesPrototypeInfo)receiverFormBlock.Clone());

            target.Comp.EnzymesPrototypes = appliedInfo;
        }

        Dirty(target.Owner, target.Comp);
        ChangeDna(target);

        // Identity is recovered from the stored EI, never from a client-supplied name.
        if (!string.IsNullOrWhiteSpace(profile.GeneticIdentityName))
            _metaData.SetEntityName(target.Owner, profile.GeneticIdentityName);

        if (_eiMutation.IsMutatable(target.Owner) && hasTraumaState)
        {
            var mutationData = _eiMutation.GetMutatableData(target.Owner);
            mutationData.Dormant.Clear();
            mutationData.Dormant.AddRange(dormant);
            mutationData.Mutations.Clear();
            mutationData.Mutations.AddRange(active);

            if (!_eiMutation.LoadMutatableData(target.Owner, mutationData))
                return false;

            // Automatic loading does not maintain scanned sequence state. Rebuild it from
            // the applied organism state instead of carrying discovery data inside the EI.
            RemComp<ScannedGenomeComponent>(target.Owner);
            _eiGenome.ScanGenome(target.Owner);
        }

        // Mutation reconstruction can mutate forensics DNA as an implementation detail.
        // Restore the donor DNA last because it is part of the captured EI.
        if (!string.IsNullOrEmpty(profile.Dna) && HasComp<DnaComponent>(target.Owner))
            _eiMutation.SetDna(target.Owner, profile.Dna);

        return true;
    }

    private bool TryResolveMutationIds(
        List<string>? stored,
        List<EntProtoId<MutationComponent>> resolved)
    {
        if (stored == null)
            return true;

        foreach (var raw in stored)
        {
            var found = false;
            foreach (var id in _eiMutation.AllMutations.Keys)
            {
                if (!string.Equals(id.ToString(), raw, StringComparison.Ordinal))
                    continue;

                resolved.Add(id);
                found = true;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }

    private bool ValidateMutationSet(List<EntProtoId<MutationComponent>> active)
    {
        var activeSet = active.ToHashSet();

        foreach (var id in active)
        {
            if (!_eiMutation.AllMutations.TryGetValue(id, out var mutation))
                return false;

            foreach (var required in mutation.Required)
            {
                if (!activeSet.Contains(required))
                    return false;
            }

            foreach (var conflict in mutation.Conflicts)
            {
                if (activeSet.Contains(conflict))
                    return false;
            }
        }

        return true;
    }
}
