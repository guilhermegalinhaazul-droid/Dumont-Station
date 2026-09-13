// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Projectiles;

/// <summary>
/// Minimal compatibility event for the imported Trauma chemistry projectile
/// detach/cleanup flow. It is intentionally a no-payload event so the host
/// Wega path can keep using the existing projectile/action object model.
/// </summary>
[ByRefEvent]
public readonly record struct EmbedDetachEvent;
