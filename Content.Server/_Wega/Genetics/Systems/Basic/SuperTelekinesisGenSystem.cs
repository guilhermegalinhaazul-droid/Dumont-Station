using Content.Server.Hands.Systems;
using Content.Shared.Genetics;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Components;

namespace Content.Server.Genetics.System;

public sealed class SuperTelekinesisGenSystem : EntitySystem
{
    [Dependency] private readonly HandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SuperTelekinesisGenComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SuperTelekinesisGenComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, SuperTelekinesisGenComponent component, ComponentStartup args)
    {
        if (!HasComp<HandsComponent>(uid))
            return;

        _hands.AddHand(uid, component.HandId, HandLocation.Left);

        var item = Spawn(component.ItemPrototype, Transform(uid).Coordinates);
        component.TelekinesisItem = item;

        if (_hands.TryPickup(uid, item, component.HandId, checkActionBlocker: false))
            EnsureComp<UnremoveableComponent>(item);
        else
            QueueDel(item);
    }

    private void OnShutdown(Entity<SuperTelekinesisGenComponent> entity, ref ComponentShutdown args)
    {
        if (entity.Comp.TelekinesisItem is { Valid: true } item)
            QueueDel(item);

        _hands.RemoveHand(entity.Owner, entity.Comp.HandId);
    }
}
