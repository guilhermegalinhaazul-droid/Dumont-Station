// SPDX-FileCopyrightText: 2025 GabyChangelog <agentepanela2@gmail.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 MarkerWicker <markerWicker@proton.me>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-FileCopyrightText: 2025 SolsticeOfTheWinter <solsticeofthewinter@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._EinsteinEngines.Forensics.Systems;
using Content.Shared._EinsteinEngines.Forensics;
using Content.Shared.Forensics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Client.Player;
using Robust.Shared.Map;

namespace Content.Client._EinsteinEngines.Forensics;

public sealed class ScentTrackerSystem : SharedScentTrackerSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Goobstation - move trycomp, scent = empty outside of the while loop
        // If the player can't track scents, continuing beyond this point is a waste of processing power.
        if (!TryComp<ScentTrackerComponent>(_playerManager.LocalEntity, out var scentcomp) || scentcomp.Scent == string.Empty)
            return;

        // Restrict the scan to entities with a transform. This avoids trying to
        // render a marker for detached/held forensic records and lets us check
        // the map before spawning a client-side effect.
        var query = EntityQueryEnumerator<ForensicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var transform))
        {
            if (scentcomp.Scent != comp.Scent
                || _timing.CurTime <= comp.TargetTime
                || transform.MapID == MapId.Nullspace)
                continue;

            comp.TargetTime = _timing.CurTime + TimeSpan.FromSeconds(0.75f);
            var coordinates = _transform.GetMapCoordinates(uid);
            Spawn("ScentTrackEffect", coordinates.Offset(_random.NextVector2(0.08f) + new Vector2(0f, 0.35f)));
        }
    }
}
