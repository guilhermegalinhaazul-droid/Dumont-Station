using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Actions;
using Content.Shared.Genetics;
using Content.Shared._Goobstation.Wizard.Mutate;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Genetics.System;

public sealed class LaserEyesGenSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly SoundSpecifier LaserShotSound =
        new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/laser_cannon.ogg");

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
        ent.Comp.GunEntity = Spawn("GeneticLaserEyesGun", Transform(ent).Coordinates);
        // Keep the private gun at the owner so its predicted shot sound stays
        // audible after the owner moves away from the spawn point.
        _transform.SetParent(ent.Comp.GunEntity.Value, ent.Owner);
    }

    private void OnShutdown(Entity<LaserEyesGenComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Comp.ActionEntity);
        if (ent.Comp.GunEntity is { } gun && Exists(gun))
            QueueDel(gun);
    }

    private void OnShoot(Entity<LaserEyesGenComponent> ent, ref ActionGunShootEvent args)
    {
        // Both genetics actions raise ActionGunShootEvent on their owner.
        // Only the action that was actually pressed may consume the event.
        if (args.Handled || args.Action.Owner != ent.Comp.ActionEntity)
            return;

        args.Handled = true;

        if (ent.Comp.GunEntity is not { } gunEntity ||
            !TryComp<GunComponent>(gunEntity, out var gun))
            return;

        _gun.AttemptShoot(ent, gunEntity, gun, args.Target);
        // Genetic eyes use a hidden gun entity. Play the shot from the gene
        // owner so the sound is always delivered to the firing client and
        // nearby listeners, just like the laser raptor's eye weapon.
        _audio.PlayPvs(LaserShotSound, ent.Owner);
    }

}
