using System.Linq;
using Content.Shared._Starlight.CollectiveMind;
using Content.Shared.Genetics;

namespace Content.Server.Genetics.System;

/// <summary>
/// The former remote-viewing gene now grants the private genetic hive-mind
/// channel. The regular collective-mind chat implementation handles the
/// <c>+c</c> prefix and keeps communication limited to matching gene holders.
/// </summary>
public sealed class RemoteViewingGenSystem : EntitySystem
{
    private const string GeneticMind = "Geneticmind";

    public override void Initialize()
    {
        SubscribeLocalEvent<RemoteViewingGenComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<RemoteViewingGenComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(Entity<RemoteViewingGenComponent> entity, ref ComponentInit args)
    {
        var mind = EnsureComp<CollectiveMindComponent>(entity.Owner);
        mind.Channels.Add(GeneticMind);
        mind.DefaultChannel ??= GeneticMind;
        Dirty(entity.Owner, mind);
    }

    private void OnShutdown(Entity<RemoteViewingGenComponent> entity, ref ComponentShutdown args)
    {
        if (!TryComp<CollectiveMindComponent>(entity.Owner, out var mind))
            return;

        mind.Channels.Remove(GeneticMind);
        if (mind.DefaultChannel == GeneticMind)
            mind.DefaultChannel = mind.Channels.FirstOrDefault();

        Dirty(entity.Owner, mind);
    }
}
