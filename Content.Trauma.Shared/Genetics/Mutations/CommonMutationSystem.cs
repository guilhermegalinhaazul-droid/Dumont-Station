// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using System.Collections.Generic;

namespace Content.Trauma.Shared.Genetics.Mutations;

public sealed class MutatableData
{
    public List<EntProtoId<MutationComponent>> Dormant { get; }
    public List<EntProtoId<MutationComponent>> Mutations { get; }

    public MutatableData(List<EntProtoId<MutationComponent>> dormant, List<EntProtoId<MutationComponent>> mutations)
    {
        Dormant = dormant;
        Mutations = mutations;
    }
}

public class CommonMutationSystem : EntitySystem
{
    public virtual MutatableData GetMutatableData(EntityUid mob)
        => new(new(), new());

    public virtual bool LoadMutatableData(EntityUid mob, MutatableData data)
        => false;
}
