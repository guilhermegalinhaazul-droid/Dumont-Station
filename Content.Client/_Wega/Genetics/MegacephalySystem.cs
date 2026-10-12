// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Genetics;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using System.Numerics;

namespace Content.Client.Genetics;

/// <summary>
///     Enlarges only the visual layers belonging to a humanoid's head.
/// </summary>
public sealed class MegacephalySystem : EntitySystem
{
    private static readonly HumanoidVisualLayers[] HeadLayers =
    [
        HumanoidVisualLayers.Head,
        HumanoidVisualLayers.Face,
        HumanoidVisualLayers.Snout,
        HumanoidVisualLayers.HeadSide,
        HumanoidVisualLayers.HeadTop,
        HumanoidVisualLayers.Eyes,
        HumanoidVisualLayers.Hair,
        HumanoidVisualLayers.FacialHair,
    ];

    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MegacephalyComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MegacephalyComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(Entity<MegacephalyComponent> ent, ref ComponentInit args)
    {
        ApplyScale(ent.Owner, 1.6f);
    }

    private void OnRemove(Entity<MegacephalyComponent> ent, ref ComponentRemove args)
    {
        ApplyScale(ent.Owner, 1f);
    }

    private void ApplyScale(EntityUid uid, float scale)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        foreach (var layer in HeadLayers)
            _sprite.LayerSetScale((uid, sprite), layer, new Vector2(scale, scale));
    }
}
