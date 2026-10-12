using Content.Shared.Genetics;
using Content.Shared.Actions;
using Content.Shared.Weapons.Ranged.Components;
using Content.Server.Weapons.Ranged.Systems;

namespace Content.Server.Genetics.System;

public sealed class AcidSpitGenSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly GunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AcidSpitGenComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AcidSpitGenComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AcidSpitGenComponent, ActionGunShootEvent>(OnShoot);
    }

    private void OnInit(Entity<AcidSpitGenComponent> ent, ref ComponentInit args)
    {
        ent.Comp.ActionEntity = _actions.AddAction(ent, ent.Comp.ActionId);
        ent.Comp.GunEntity = Spawn("GeneticAcidSpitGun", Transform(ent).Coordinates);
    }

    private void OnShutdown(Entity<AcidSpitGenComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Comp.ActionEntity);
        if (ent.Comp.GunEntity is { } gun && Exists(gun))
            QueueDel(gun);
    }

    private void OnShoot(Entity<AcidSpitGenComponent> ent, ref ActionGunShootEvent args)
    {
        if (args.Handled || args.Action.Owner != ent.Comp.ActionEntity)
            return;

        args.Handled = true;

        if (ent.Comp.GunEntity is not { } gunEntity ||
            !TryComp<GunComponent>(gunEntity, out var gun))
            return;

        _gun.AttemptShoot(ent, gunEntity, gun, args.Target);
    }
}
