// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Genetics;

namespace Content.Server.Genetics.System;

public sealed partial class DnaModifierSystem
{
    /// <summary>
    /// Activates/deactivates one Wega structural gene by changing the real structural-enzyme
    /// code and then running the existing Wega application pipeline.
    /// </summary>
    public bool TrySetStructuralGeneActive(EntityUid uid, string geneId, bool active)
    {
        if (!TryComp<DnaModifierComponent>(uid, out var dna)
            || dna.EnzymesPrototypes == null
            || !_prototype.TryIndex<StructuralEnzymesPrototype>(geneId, out var prototype)
            || prototype.TypeDeviation == EnzymesType.Disease)
        {
            return false;
        }

        var enzyme = dna.EnzymesPrototypes.FirstOrDefault(entry => entry.EnzymesPrototypeId == geneId);
        if (enzyme == null || enzyme.Order == 55)
            return false;

        if (IsStructuralEnzymeActive(uid, geneId) == active)
            return true;

        enzyme.HexCode = active
            ? GetHexCodeForType(prototype.TypeDeviation)
            : new[] { "0", "0", "0" };

        Dirty(uid, dna);
        TryChangeStructuralEnzymes((uid, dna));

        return IsStructuralEnzymeActive(uid, geneId) == active;
    }
}
