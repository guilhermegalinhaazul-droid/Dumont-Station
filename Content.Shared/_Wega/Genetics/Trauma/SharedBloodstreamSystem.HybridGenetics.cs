// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Body.Components;

namespace Content.Shared.Body.Systems;

public abstract partial class SharedBloodstreamSystem
{
    /// <summary>
    /// Controlled mutation hook for genetics effects that alter natural blood regeneration.
    /// Keeping the write here preserves BloodstreamComponent access rules.
    /// </summary>
    public void SetGeneticBloodRefreshAmount(Entity<BloodstreamComponent> ent, FixedPoint2 amount)
    {
        ent.Comp.BloodRefreshAmount = amount;
        DirtyField(ent, ent.Comp, nameof(BloodstreamComponent.BloodRefreshAmount));
    }
}
