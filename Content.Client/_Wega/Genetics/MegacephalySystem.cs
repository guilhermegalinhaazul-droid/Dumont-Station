// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Inventory;
using Content.Shared.Clothing;
using Content.Shared.Genetics;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using System.Numerics;

namespace Content.Client.Genetics;

/// <summary>
///     Enlarges the head and every visual layer attached to it without changing
///     the size of the character's body.
/// </summary>
public sealed class MegacephalySystem : EntitySystem
{
    private const float HeadScale = 1.6f;
    private const float HeadDrop = 0.12f;

    private static readonly HumanoidVisualLayers[] HeadLayers =
    [
        HumanoidVisualLayers.Special,
        HumanoidVisualLayers.Head,
        HumanoidVisualLayers.Face,
        HumanoidVisualLayers.Snout,
        HumanoidVisualLayers.HeadSide,
        HumanoidVisualLayers.HeadTop,
        HumanoidVisualLayers.Eyes,
        HumanoidVisualLayers.Hair,
        HumanoidVisualLayers.FacialHair,
    ];

    private static readonly string[] HeadSlots = ["head", "mask", "eyes", "ears"];

    private readonly Dictionary<EntityUid, Dictionary<ISpriteLayer, LayerState>> _originalLayers = new();

    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MegacephalyComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MegacephalyComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
    }

    private void OnInit(Entity<MegacephalyComponent> ent, ref ComponentInit args)
    {
        ApplyScale(ent.Owner);
    }

    private void OnRemove(Entity<MegacephalyComponent> ent, ref ComponentRemove args)
    {
        Restore(ent.Owner);
    }

    private void OnEquipmentVisualsUpdated(EntityUid equipment, ref EquipmentVisualsUpdatedEvent args)
    {
        if (!HeadSlots.Contains(args.Slot) || !HasComp<MegacephalyComponent>(args.Equipee))
            return;

        ApplyScale(args.Equipee);
    }

    private void ApplyScale(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!_originalLayers.TryGetValue(uid, out var originals))
        {
            originals = new Dictionary<ISpriteLayer, LayerState>();
            _originalLayers.Add(uid, originals);
        }

        foreach (var index in FindHeadLayerIndices(uid, sprite))
        {
            var layer = sprite[index];
            if (!originals.ContainsKey(layer))
                originals[layer] = new LayerState(layer.Scale, GetOffset(layer));

            var original = originals[layer];
            _sprite.LayerSetScale((uid, sprite), index, original.Scale * HeadScale);
            _sprite.LayerSetOffset((uid, sprite), index, original.Offset + new Vector2(0f, -HeadDrop));
        }
    }

    private IEnumerable<int> FindHeadLayerIndices(EntityUid uid, SpriteComponent sprite)
    {
        var indices = new HashSet<int>();

        foreach (var layer in HeadLayers)
        {
            if (_sprite.LayerMapTryGet((uid, sprite), layer, out var index, false))
                indices.Add(index);
        }

        if (TryComp<InventorySlotsComponent>(uid, out var inventorySlots))
        {
            foreach (var slot in HeadSlots)
            {
                if (!inventorySlots.VisualLayerKeys.TryGetValue(slot, out var keys))
                    continue;

                foreach (var key in keys)
                {
                    if (_sprite.LayerMapTryGet((uid, sprite), key, out var index, false))
                        indices.Add(index);
                }
            }
        }

        return indices;
    }

    private void Restore(EntityUid uid)
    {
        if (!_originalLayers.Remove(uid, out var originals))
            return;

        foreach (var (layer, original) in originals)
        {
            layer.Scale = original.Scale;
            layer.Offset = original.Offset;
        }
    }

    private static Vector2 GetOffset(ISpriteLayer layer)
    {
        return layer is SpriteComponent.Layer concrete ? concrete.Offset : Vector2.Zero;
    }

    private readonly record struct LayerState(Vector2 Scale, Vector2 Offset);
}
