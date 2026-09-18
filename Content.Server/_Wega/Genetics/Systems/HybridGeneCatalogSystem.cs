// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Genetics;
using Content.Shared.Genetics.Systems;
using Content.Shared.Genetics.UI;
using Content.Trauma.Shared.Genetics.Mutations;
using Robust.Shared.Prototypes;

namespace Content.Server.Genetics.System;

/// <summary>
/// Read-only projection of the real Wega structural-enzyme and Trauma mutation state.
/// It owns no gene state of its own.
/// </summary>
public sealed class HybridGeneCatalogSystem : EntitySystem
{
    [Dependency] private readonly DnaModifierSystem _dnaModifier = default!;
    [Dependency] private readonly StructuralEnzymesIndexerSystem _enzymesIndexer = default!;
    [Dependency] private readonly MutationSystem _mutation = default!;
    [Dependency] private readonly ScannedGenomeSystem _scannedGenome = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public List<GeneCatalogEntry> BuildCatalog(EntityUid? scannedBody = null)
    {
        var catalog = new List<GeneCatalogEntry>();
        var byCanonicalKey = new Dictionary<string, GeneCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        // StructuralEnzymesIndexerSystem remains the only Wega ordering/index authority.
        foreach (var enzymeInfo in _enzymesIndexer.GetAllEnzymesPrototypes())
        {
            if (string.IsNullOrEmpty(enzymeInfo.EnzymesPrototypeId)
                || !_prototypeManager.TryIndex<StructuralEnzymesPrototype>(enzymeInfo.EnzymesPrototypeId, out var prototype)
                || prototype.TypeDeviation == EnzymesType.Disease)
            {
                continue;
            }

            var wegaId = prototype.ID;
            var canonicalKey = GeneCanonicalKeys.Get(wegaId, prototype.CanonicalKey);
            if (byCanonicalKey.ContainsKey(canonicalKey))
                continue;

            var entry = new GeneCatalogEntry
            {
                CanonicalKey = canonicalKey,
                GeneId = wegaId,
                GeneName = GetWegaName(prototype),
                Origin = "Wega",
                WegaGeneId = wegaId,
                Discovered = true,
                Active = scannedBody is { } body && _dnaModifier.IsStructuralEnzymeActive(body, wegaId),
                Available = true,
            };

            catalog.Add(entry);
            byCanonicalKey.Add(canonicalKey, entry);
        }

        // MutationSystem.AllMutations is the only mutation index used here.
        foreach (var (mutationId, mutationComponent) in _mutation.AllMutations
                     .OrderBy(pair => pair.Key.ToString()))
        {
            var traumaId = mutationId.ToString();
            var canonicalKey = GeneCanonicalKeys.Get(traumaId, mutationComponent.CanonicalKey);
            var mutationData = _mutation.GetRoundData(mutationId);
            var discovered = mutationData?.Discovered == true;
            var active = IsTraumaActive(scannedBody, mutationId);

            if (byCanonicalKey.TryGetValue(canonicalKey, out var wegaEntry))
            {
                // Equivalent systems share one visible row. A Wega structural gene is already
                // identified by Wega, so attaching an undiscovered Trauma implementation must
                // not make that known gene appear unknown.
                wegaEntry.TraumaMutationId = traumaId;
                wegaEntry.Origin = "Wega+Trauma";
                wegaEntry.Discovered |= discovered;
                wegaEntry.Active |= active;
                wegaEntry.Available = true;
                wegaEntry.CanSequence = false;
                continue;
            }

            var entry = new GeneCatalogEntry
            {
                CanonicalKey = canonicalKey,
                GeneId = traumaId,
                GeneName = discovered
                    ? GetTraumaName(traumaId)
                    : $"Gene não identificado #{mutationData?.Number ?? 0}",
                Origin = "Trauma",
                TraumaMutationId = traumaId,
                Discovered = discovered,
                Active = active,
                Available = discovered,
                CanSequence = !discovered && HasSequence(scannedBody, mutationId),
            };

            catalog.Add(entry);
            byCanonicalKey.Add(canonicalKey, entry);
        }

        return catalog;
    }

    public GeneCatalogEntry? FindByCanonicalKey(EntityUid? body, string canonicalKey)
        => BuildCatalog(body).FirstOrDefault(entry =>
            string.Equals(entry.CanonicalKey, canonicalKey, StringComparison.OrdinalIgnoreCase));

    private string GetWegaName(StructuralEnzymesPrototype prototype)
    {
        // Wega's prototype currently stores a localized effect message rather than a dedicated
        // display-name key. Prefer a sibling *-name string when one exists, then fall back to ID.
        var message = prototype.Message;
        if (!string.IsNullOrWhiteSpace(message)
            && message.EndsWith("-message", StringComparison.Ordinal))
        {
            var nameKey = message[..^"-message".Length] + "-name";
            if (Loc.TryGetString(nameKey, out var name))
                return name;
        }

        return prototype.ID;
    }

    private string GetTraumaName(string id)
    {
        if (_prototypeManager.TryIndex<EntityPrototype>(id, out var prototype))
            return Loc.GetString(prototype.Name);

        return id;
    }

    private bool HasSequence(EntityUid? scannedBody, EntProtoId<MutationComponent> mutationId)
    {
        if (scannedBody is not { } body)
            return false;

        for (uint index = 0; ; index++)
        {
            var sequence = _scannedGenome.GetSequence(body, index);
            if (sequence == null)
                return false;

            if (sequence.Mutation == mutationId)
                return true;
        }
    }

    private bool IsTraumaActive(EntityUid? scannedBody, EntProtoId<MutationComponent> mutationId)
    {
        if (scannedBody is not { } body || _mutation.GetMutatable(body) is not { } mutatable)
            return false;

        return _mutation.HasMutation(mutatable.AsNullable(), mutationId);
    }
}
