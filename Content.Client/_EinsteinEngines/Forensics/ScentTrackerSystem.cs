// SPDX-FileCopyrightText: 2025 GabyChangelog <agentepanela2@gmail.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 MarkerWicker <markerWicker@proton.me>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-FileCopyrightText: 2025 SolsticeOfTheWinter <solsticeofthewinter@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._EinsteinEngines.Forensics.Systems;
using Content.Shared._EinsteinEngines.Forensics;
using Content.Shared.Forensics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Client.Player;
using Robust.Client.GameObjects;
using Robust.Shared.Map;

namespace Content.Client._EinsteinEngines.Forensics;

public sealed class ScentTrackerSystem : SharedScentTrackerSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    // Scent markers are client-side effects. Keep the ones created by this
    // tracker so toggling the action can hide and restore the existing trail
    // without resetting its five-minute lifetime.
    private readonly HashSet<EntityUid> _trackedEffects = new();
    private string _lastScent = string.Empty;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var localEntity = _playerManager.LocalEntity;
        CleanupUnownedEffects(localEntity);

        if (localEntity is not { } trackerEntity)
            return;

        // Goobstation - move trycomp, scent = empty outside of the while loop
        // If the player can't track scents, continuing beyond this point is a waste of processing power.
        if (!TryComp<ScentTrackerComponent>(trackerEntity, out var scentcomp))
        {
            ClearTrackedEffects();
            _lastScent = string.Empty;
            return;
        }

        // Changing the tracked target must not leave the previous trail on
        // screen. Toggling visibility only hides the existing markers so they
        // can reappear when the action is enabled again.
        if (_lastScent != scentcomp.Scent || scentcomp.Scent == string.Empty)
        {
            ClearTrackedEffects();
            _lastScent = scentcomp.Scent;
        }

        SetTrackedEffectsVisible(scentcomp.VisualsEnabled);

        if (!scentcomp.VisualsEnabled || scentcomp.Scent == string.Empty)
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

            // Leave enough time between markers to create a readable trail
            // without spawning one effect every update tick. Each marker is
            // intentionally persistent for the full scent-tracking window.
            comp.TargetTime = _timing.CurTime + TimeSpan.FromSeconds(1.5f);
            var coordinates = _transform.GetMapCoordinates(uid);
            var effect = Spawn("ScentTrackEffect", coordinates.Offset(_random.NextVector2(0.18f) + new Vector2(0f, 0.4f)));
            var marker = EnsureComp<ScentTrackEffectComponent>(effect);
            marker.Tracker = trackerEntity;
            marker.Scent = scentcomp.Scent;
            _trackedEffects.Add(effect);
        }
    }

    private void CleanupUnownedEffects(EntityUid? localEntity)
    {
        var query = EntityQueryEnumerator<ScentTrackEffectComponent>();
        while (query.MoveNext(out var uid, out var marker))
        {
            if (localEntity is not { } owner || marker.Tracker != owner)
                QueueDel(uid);
        }
    }

    private void ClearTrackedEffects()
    {
        foreach (var effect in _trackedEffects)
        {
            if (Exists(effect))
                QueueDel(effect);
        }

        _trackedEffects.Clear();
    }

    private void SetTrackedEffectsVisible(bool visible)
    {
        foreach (var effect in _trackedEffects)
        {
            if (!Exists(effect) || !TryComp<SpriteComponent>(effect, out var sprite))
                continue;

            sprite.Visible = visible;
        }
    }
}
