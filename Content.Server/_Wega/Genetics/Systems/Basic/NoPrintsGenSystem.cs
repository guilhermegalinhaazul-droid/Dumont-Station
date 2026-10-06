using Content.Shared.Forensics.Components;
using Content.Shared.Genetics;

namespace Content.Server.Genetics.System;

public sealed class NoPrintsGenSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NoPrintsGenComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<NoPrintsGenComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(Entity<NoPrintsGenComponent> ent, ref ComponentInit args)
    {
        if (TryComp<FingerprintComponent>(ent, out var fingerprint))
        {
            if (fingerprint.Fingerprint is { } prints)
                ent.Comp.OldPrints = prints;

            RemComp<FingerprintComponent>(ent);
        }
    }

    private void OnShutdown(Entity<NoPrintsGenComponent> ent, ref ComponentShutdown args)
    {
        // The entity may be shutting down because a gene transformation is
        // replacing it. Components cannot be added after termination starts.
        if (TerminatingOrDeleted(ent))
            return;

        EnsureComp<FingerprintComponent>(ent).Fingerprint = ent.Comp.OldPrints;
    }
}
