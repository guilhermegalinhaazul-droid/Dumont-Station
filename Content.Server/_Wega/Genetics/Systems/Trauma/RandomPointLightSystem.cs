// SPDX-License-Identifier: AGPL-3.0-or-later
// Adapted from Trauma's RandomPointLightSystem for components added by Wega genes.
using System.Numerics;
using Robust.Shared.Random;
namespace Content.Server.Genetics.System;
public sealed partial class RandomPointLightSystem : EntitySystem
{
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RandomPointLightComponent, ComponentStartup>(OnStartup);
    }
    private void OnStartup(Entity<RandomPointLightComponent> ent, ref ComponentStartup args)
    {
        _light.SetRadius(ent, _random.NextFloat(ent.Comp.MinRadius, ent.Comp.MaxRadius));
        _light.SetEnergy(ent, _random.NextFloat(ent.Comp.MinEnergy, ent.Comp.MaxEnergy));
        _light.SetColor(ent, Color.FromHsv(new Vector4(_random.NextFloat(), _random.NextFloat(), _random.NextFloat(0.5f, 1f), 1f)));
    }
}
