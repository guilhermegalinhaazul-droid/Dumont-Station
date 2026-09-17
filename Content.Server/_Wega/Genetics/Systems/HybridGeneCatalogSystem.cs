// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Genetics;
using Content.Shared.Genetics.Systems;
using Content.Shared.Genetics.UI;
using Content.Trauma.Shared.Genetics.Mutations;
using Robust.Shared.Prototypes;

namespace Content.Server.Genetics.System;

/// <summary>
/// Builds the unified Wega + Trauma gene catalog without owning mutation, sequencing,
/// discovery, or structural-enzyme state. Those remain owned by their existing systems.
/// </summary>
public sealed class HybridGeneCatalogSystem : EntitySystem
{
    [Dependency] private readonly StructuralEnzymesIndexerSystem _enzymesIndexer = default!;
    [Dependency] private readonly MutationSystem _mutation = default!;
    [Dependency] private readonly ScannedGenomeSystem _scannedGenome = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    /// <summary>
    /// Builds the round catalog. Wega order is preserved, Wega diseases are omitted, and
    /// equivalent Trauma mutations are folded into their Wega entry through GeneCanonicalKeys.
    /// Trauma-only mutations are appended and remain present even before discovery.
    /// </summary>
    public List<GeneCatalogEntry> BuildCatalog(EntityUid? scannedBody = null)
    {
        var catalog = new List<GeneCatalogEntry>();
        var byCanonicalKey = new Dictionary<string, GeneCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var enzymeInfo in _enzymesIndexer.GetAllEnzymesPrototypes())
        {
            if (string.IsNullOrEmpty(enzymeInfo.EnzymesPrototypeId)
                || !_prototypeManager.TryIndex<StructuralEnzymesPrototype>(enzymeInfo.EnzymesPrototypeId, out var prototype)
                || prototype.TypeDeviation == EnzymesType.Disease)
            {
                continue;
            }

            var wegaId = prototype.ID;
            var canonicalKey = GeneCanonicalKeys.Get(wegaId);
            if (byCanonicalKey.ContainsKey(canonicalKey))
                continue;

            var entry = new GeneCatalogEntry
            {
                CanonicalKey = canonicalKey,
                GeneId = wegaId,
                GeneName = wegaId,
                Origin = "Wega",
                WegaGeneId = wegaId,
                Discovered = true,
                Available = true,
            };

            catalog.Add(entry);
            byCanonicalKey.Add(canonicalKey, entry);
        }

        foreach (var mutationId in _mutation.AllMutations.Keys.OrderBy(id => id.ToString()))
        {
            var traumaId = mutationId.ToString();
            var canonicalKey = GeneCanonicalKeys.Get(traumaId);
            var discovered = _mutation.GetRoundData(mutationId)?.Discovered == true;
            var canSequence = !discovered && HasSequence(scannedBody, mutationId);
            var active = IsActive(scannedBody, mutationId);

            if (byCanonicalKey.TryGetValue(canonicalKey, out var wegaEntry))
            {
                // Wega remains the visible/primary identity, while discovery/availability of
                // the Trauma-backed gene remains tied to the real MutationData state.
                wegaEntry.TraumaMutationId = traumaId;
                wegaEntry.Origin = "Wega+Trauma";
                wegaEntry.Discovered = discovered;
                wegaEntry.Active |= active;
                wegaEntry.Available = discovered;
                wegaEntry.CanSequence = canSequence;
                continue;
            }

            var entry = new GeneCatalogEntry
            {
                CanonicalKey = canonicalKey,
                GeneId = traumaId,
                GeneName = traumaId,
                Origin = "Trauma",
                TraumaMutationId = traumaId,
                Discovered = discovered,
                Active = active,
                Available = discovered,
                CanSequence = canSequence,
            };

            catalog.Add(entry);
            byCanonicalKey.Add(canonicalKey, entry);
        }

        return catalog;
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

    private bool IsActive(EntityUid? scannedBody, EntProtoId<MutationComponent> mutationId)
    {
        if (scannedBody is not { } body || _mutation.GetMutatable(body) is not { } mutatable)
            return false;

        return _mutation.HasMutation(mutatable.AsNullable(), mutationId);
    }
}
