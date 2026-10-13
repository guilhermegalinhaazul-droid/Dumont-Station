// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Inventory;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
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
        // Humanoid layers (including animal heads) are rebuilt after the
        // appearance state arrives. Reapply after that rebuild so snouts,
        // hair, horns and other attached layers do not lose their scale.
        SubscribeLocalEvent<HumanoidAppearanceComponent, AfterAutoHandleStateEvent>(OnHumanoidAppearanceState);
        SubscribeLocalEvent<ClothingComponent, EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
    }

    private void OnInit(Entity<MegacephalyComponent> ent, ref ComponentInit args)
    {
        ApplyScale(ent.Owner);
    }

    private void OnRemove(Entity<MegacephalyComponent> ent, ref ComponentRemove args)
    {
        Restore(ent.Owner);
    }

    private void OnHumanoidAppearanceState(Entity<HumanoidAppearanceComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (HasComp<MegacephalyComponent>(ent.Owner))
            ApplyScale(ent.Owner);
    }

    private void OnEquipmentVisualsUpdated(Entity<ClothingComponent> equipment, ref EquipmentVisualsUpdatedEvent args)
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

        var indices = FindHeadLayerIndices(uid, sprite).ToArray();
        if (indices.Length == 0)
            return;

        // Sprite layer offsets are translations and are not affected by a
        // layer's Scale. Scale each attached layer around the original head
        // anchor, otherwise accessories stay behind while the head grows.
        var headAnchor = Vector2.Zero;
        if (_sprite.LayerMapTryGet((uid, sprite), HumanoidVisualLayers.Head, out var headIndex, false))
        {
            var headLayer = sprite[headIndex];
            if (!originals.ContainsKey(headLayer))
                originals[headLayer] = new LayerState(headLayer.Scale, GetOffset(headLayer));
            headAnchor = originals[headLayer].Offset;
        }

        foreach (var index in indices)
        {
            var layer = sprite[index];
            if (!originals.ContainsKey(layer))
                originals[layer] = new LayerState(layer.Scale, GetOffset(layer));

            var original = originals[layer];
            _sprite.LayerSetScale((uid, sprite), index, original.Scale * HeadScale);
            var relativeOffset = original.Offset - headAnchor;
            var scaledOffset = headAnchor + relativeOffset * HeadScale;
            _sprite.LayerSetOffset((uid, sprite), index, scaledOffset + new Vector2(0f, -HeadDrop));
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
            if (layer is not SpriteComponent.Layer concrete)
                continue;

            _sprite.LayerSetScale(concrete, original.Scale);
            _sprite.LayerSetOffset(concrete, original.Offset);
        }
    }

    private static Vector2 GetOffset(ISpriteLayer layer)
    {
        return layer is SpriteComponent.Layer concrete ? concrete.Offset : Vector2.Zero;
    }

    private readonly record struct LayerState(Vector2 Scale, Vector2 Offset);
}
