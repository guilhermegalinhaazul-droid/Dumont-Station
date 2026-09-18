// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Trauma.Shared.Genetics.Mutations;

/// <summary>
/// Minimal data carrier required by the imported MutationSystem APIs.
/// This is not a second mutation system; it only mirrors the existing Trauma DTO whose
/// containing CommonMutationSystem file is intentionally not linked into Content.Shared.
/// </summary>
public sealed class MutatableData
{
    public List<EntProtoId<MutationComponent>> Dormant { get; }
    public List<EntProtoId<MutationComponent>> Mutations { get; }

    public MutatableData(
        List<EntProtoId<MutationComponent>> dormant,
        List<EntProtoId<MutationComponent>> mutations)
    {
        Dormant = dormant;
        Mutations = mutations;
    }
}
