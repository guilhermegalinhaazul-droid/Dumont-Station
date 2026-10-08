using Content.Shared.Genetics;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Timing;

namespace Content.Server.Genetics.System;

public sealed class AcidSpitGenSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AcidSpitGenComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<AcidSpitGenComponent> ent, ref ComponentInit args)
    {
        if (TryComp<RechargeBasicEntityAmmoComponent>(ent, out var recharge))
            recharge.NextCharge = _timing.CurTime;
    }
}
