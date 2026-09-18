// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Body.Events;

/// <summary>
/// Allows systems such as genetics mutations to modify positive changes to bleed rate
/// before they are applied by the current bloodstream implementation.
/// </summary>
[ByRefEvent]
public record struct BleedModifierEvent
{
    public float BleedAmount;

    public BleedModifierEvent(float bleedAmount)
    {
        BleedAmount = bleedAmount;
    }
}
