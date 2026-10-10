using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Actions;
using Content.Shared.Genetics;
using Content.Shared._Goobstation.Wizard.Mutate;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Genetics.System;

public sealed class LaserEyesGenSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly GunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LaserEyesGenComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<LaserEyesGenComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LaserEyesGenComponent, ActionGunShootEvent>(OnShoot);
    }

    private void OnInit(Entity<LaserEyesGenComponent> ent, ref ComponentInit args)
    {
        ent.Comp.ActionEntity = _actions.AddAction(ent, ent.Comp.ActionId);
        var gun = EnsureComp<GunComponent>(ent);
        _gun.SetFireRate(gun, 1.5f);
        _gun.SetUseKey(gun, false);
        _gun.SetClumsyProof(gun, true);
        _gun.RefreshModifiers((ent, gun));

        var hitscan = EntityManager.ComponentFactory.GetComponent<BasicHitscanAmmoProviderComponent>();
        hitscan.Proto = new ProtoId<HitscanPrototype>("RedHeavyLaser");
        AddComp(ent, hitscan, true);
    }

    private void OnShutdown(Entity<LaserEyesGenComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Comp.ActionEntity);
    }

    private void OnShoot(Entity<LaserEyesGenComponent> ent, ref ActionGunShootEvent args)
    {
        if (TryComp<GunComponent>(ent, out var gun))
            _gun.AttemptShoot(ent, ent, gun, args.Target);

        args.Handled = true;
    }

}
