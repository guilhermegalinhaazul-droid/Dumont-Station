using System.Linq;
using Content.Shared.Genetics.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Genetics;
using Content.Shared.Interaction;
using Robust.Server.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Genetics.System;

public sealed partial class DnaModifierSystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;

    private static readonly ProtoId<DamageTypePrototype> Damage = "Poison";

    private void InitializeInjector()
    {
        SubscribeLocalEvent<DnaModifierInjectorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DnaModifierInjectorComponent, DnaInjectorDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<DnaModifierCleanRandomizeComponent, ComponentStartup>(OnCleanRandomize);
        SubscribeLocalEvent<DnaModifierActivateAllComponent, ComponentStartup>(OnActivateAll);
    }

    public void OnFillingInjector(EntityUid injector, UniqueIdentifiersData? uniqueIdentifiers, List<EnzymesPrototypeInfo>? enzymesPrototypes)
    {
        if (!TryComp(injector, out DnaModifierInjectorComponent? comp))
            return;

        if (uniqueIdentifiers == null && enzymesPrototypes == null)
            return;

        comp.UniqueIdentifiers = uniqueIdentifiers != null
            ? CloneUniqueIdentifiers(uniqueIdentifiers)
            : null;

        comp.EnzymesPrototypes = enzymesPrototypes != null
            ? CloneEnzymesPrototypes(enzymesPrototypes)
            : null;

        Dirty(injector, comp);
    }

    private void OnAfterInteract(Entity<DnaModifierInjectorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        var user = args.User;
        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(5f),
            new DnaInjectorDoAfterEvent(), args.Used, target: args.Target.Value, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            MovementThreshold = 0.01f,
            NeedHand = false
        });

        args.Handled = true;
    }

    private void OnDoAfter(EntityUid uid, DnaModifierInjectorComponent component, DnaInjectorDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !args.Used.HasValue || !args.Target.HasValue)
            return;

        TryDoInject((uid, component), args.Target.Value);

        args.Handled = true;
    }

    private bool TryDoInject(Entity<DnaModifierInjectorComponent> ent, EntityUid target)
    {
        if (ent.Comp.UniqueIdentifiers == null && ent.Comp.EnzymesPrototypes == null)
            return false;

        if (!TryComp(target, out DnaModifierComponent? dnaModifier))
            return false;

        if (ent.Comp.UniqueIdentifiers != null)
        {
            dnaModifier.UniqueIdentifiers = CloneUniqueIdentifiers(ent.Comp.UniqueIdentifiers);
        }

        if (ent.Comp.EnzymesPrototypes != null)
        {
            foreach (var incoming in ent.Comp.EnzymesPrototypes)
            {
                // Crafted genes are unlocked and activated only by the
                // combination system, never by an injector payload.
                if (IsRecipeResult(incoming.EnzymesPrototypeId))
                    continue;

                var existing = dnaModifier.EnzymesPrototypes?.FirstOrDefault(g => g.EnzymesPrototypeId == incoming.EnzymesPrototypeId);
                if (existing != null)
                    existing.Active = incoming.Active;
            }
        }

        Dirty(target, dnaModifier);

        // Applying every active structural gene can add a large number of
        // components. Defer that work until the next tick so PVS does not
        // attempt to serialize newly-created components as delta state in
        // the same tick they were created.
        Timer.Spawn(0, () =>
        {
            if (Exists(target))
                ChangeDna(target);
        });

        _audio.PlayPvs(ent.Comp.InjectSound, target);

        var damage = new DamageSpecifier { DamageDict = { { Damage, 5 } } };
        _damage.TryChangeDamage(target, damage, true);

        _entManager.DeleteEntity(ent);

        return true;
    }

    /// <summary>
    /// Generating pure SE
    /// </summary>
    private void OnCleanRandomize(Entity<DnaModifierCleanRandomizeComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<DnaModifierInjectorComponent>(ent, out var injector))
            return;

        var enzymesPrototypes = _enzymesIndexer.GetAllEnzymesPrototypes();
        var uniqueEnzymesPrototypes = new List<EnzymesPrototypeInfo>();
        foreach (var enzymePrototype in enzymesPrototypes)
        {
            var uniqueEnzyme = new EnzymesPrototypeInfo
            {
                EnzymesPrototypeId = enzymePrototype.EnzymesPrototypeId,
                Order = enzymePrototype.Order,
                Active = enzymePrototype.EnzymesPrototypeId == StructuralEnzymesIndexerSystem.SpeciesGene
            };

            uniqueEnzymesPrototypes.Add(uniqueEnzyme);
        }

        injector.EnzymesPrototypes = uniqueEnzymesPrototypes;
    }

    private void OnActivateAll(Entity<DnaModifierActivateAllComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<DnaModifierInjectorComponent>(ent, out var injector))
            return;

        var enzymesPrototypes = _enzymesIndexer.GetAllEnzymesPrototypes();
        injector.EnzymesPrototypes = enzymesPrototypes
            .Where(enzyme => !IsRecipeResult(enzyme.EnzymesPrototypeId))
            .Select(enzyme => new EnzymesPrototypeInfo
            {
                EnzymesPrototypeId = enzyme.EnzymesPrototypeId,
                Order = enzyme.Order,
                Active = true
            })
            .ToList();
    }
}
