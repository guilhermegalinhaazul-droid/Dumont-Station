// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <comedian_vs_clown@hotmail.com>
// SPDX-FileCopyrightText: 2024 0x6273 <0x40@keemail.me>
// SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2024 deathride58 <deathride58@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Inventory;

namespace Content.Shared.Eye.Blinding.Systems;

public sealed class BlurryVisionSystem : EntitySystem
{
    private static readonly HashSet<string> VisionCorrectionExceptions = new()
    {
        "ClothingEyesGlassesCheapSunglasses",
        "ClothingEyesGlassesGar",
        "ClothingEyesGlassesGarOrange",
        "ClothingEyesGlassesGarGiga"
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VisionCorrectionComponent, GotEquippedEvent>(OnGlassesEquipped);
        SubscribeLocalEvent<VisionCorrectionComponent, GotUnequippedEvent>(OnGlassesUnequipped);
        SubscribeLocalEvent<VisionCorrectionComponent, InventoryRelayedEvent<GetBlurEvent>>(OnGetBlur);
        SubscribeLocalEvent<ClothingComponent, GotEquippedEvent>(OnClothingEquipped);
        SubscribeLocalEvent<ClothingComponent, GotUnequippedEvent>(OnClothingUnequipped);
        SubscribeLocalEvent<ClothingComponent, InventoryRelayedEvent<GetBlurEvent>>(OnClothingGetBlur);
    }

    private void OnGetBlur(Entity<VisionCorrectionComponent> glasses, ref InventoryRelayedEvent<GetBlurEvent> args)
    {
        if (IsVisionCorrectionException(glasses.Owner))
            return;

        args.Args.Blur += glasses.Comp.VisionBonus;
        args.Args.CorrectionPower *= glasses.Comp.CorrectionPower;
    }

    private void OnClothingGetBlur(Entity<ClothingComponent> clothing, ref InventoryRelayedEvent<GetBlurEvent> args)
    {
        if ((clothing.Comp.Slots & SlotFlags.EYES) == 0 || IsVisionCorrectionException(clothing.Owner) ||
            HasComp<VisionCorrectionComponent>(clothing))
            return;

        args.Args.Blur += VisionCorrectionComponent.PoorVisionCorrection;
    }

    private void OnClothingEquipped(Entity<ClothingComponent> clothing, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.EYES) != 0 && !IsVisionCorrectionException(clothing.Owner))
            UpdateBlurMagnitude(args.Equipee);
    }

    private void OnClothingUnequipped(Entity<ClothingComponent> clothing, ref GotUnequippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.EYES) != 0 && !IsVisionCorrectionException(clothing.Owner))
            UpdateBlurMagnitude(args.Equipee);
    }

    private bool IsVisionCorrectionException(EntityUid entity)
        => MetaData(entity).EntityPrototype?.ID is { } id && VisionCorrectionExceptions.Contains(id);

    public void UpdateBlurMagnitude(Entity<BlindableComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        var ev = new GetBlurEvent(ent.Comp.EyeDamage);
        RaiseLocalEvent(ent, ev);

        var blur = Math.Clamp(ev.Blur, 0, BlurryVisionComponent.MaxMagnitude);
        if (blur <= 0)
        {
            RemCompDeferred<BlurryVisionComponent>(ent);
            return;
        }

        var blurry = EnsureComp<BlurryVisionComponent>(ent);
        blurry.Magnitude = blur;
        blurry.CorrectionPower = ev.CorrectionPower;
        Dirty(ent, blurry);
    }

    private void OnGlassesEquipped(Entity<VisionCorrectionComponent> glasses, ref GotEquippedEvent args)
    {
        UpdateBlurMagnitude(args.Equipee);
    }

    private void OnGlassesUnequipped(Entity<VisionCorrectionComponent> glasses, ref GotUnequippedEvent args)
    {
        UpdateBlurMagnitude(args.Equipee);
    }
}

public sealed class GetBlurEvent : EntityEventArgs, IInventoryRelayEvent
{
    public readonly float BaseBlur;
    public float Blur;
    public float CorrectionPower = BlurryVisionComponent.DefaultCorrectionPower;

    public GetBlurEvent(float blur)
    {
        Blur = blur;
        BaseBlur = blur;
    }

    public SlotFlags TargetSlots => SlotFlags.HEAD | SlotFlags.MASK | SlotFlags.EYES;
}
