using Content.Shared.Genetics;
using Content.Shared.Actions;
using Content.Shared.Weapons.Ranged.Components;
using Content.Server.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Genetics.System;

public sealed class AcidSpitGenSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

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
        if (TryComp<RechargeBasicEntityAmmoComponent>(ent, out var recharge))
            recharge.NextCharge = _timing.CurTime;
    }

    private void OnShutdown(Entity<AcidSpitGenComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Comp.ActionEntity);
    }

    private void OnShoot(Entity<AcidSpitGenComponent> ent, ref ActionGunShootEvent args)
    {
        if (TryComp<GunComponent>(ent, out var gun))
            _gun.AttemptShoot(ent, ent, gun, args.Target);

        args.Handled = true;
    }
}
