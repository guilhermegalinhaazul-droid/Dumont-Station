using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Forensics.Components;
using Content.Shared.Genetics;
using Content.Shared.Genetics.Systems;
using Content.Shared.Genetics.UI;
using Content.Shared.Interaction;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server.Genetics.System;

public sealed class PortableDnaScannerSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SharedDnaModifierSystem _dnaModifier = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PortableDnaScannerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PortableDnaScannerComponent, AfterActivatableUIOpenEvent>(OnUiOpen);
        Subs.BuiEvents<PortableDnaScannerComponent>(PortableDnaScannerUiKey.Key, subs =>
        {
            subs.Event<PortableDnaScannerSaveMessage>(OnSave);
            subs.Event<PortableDnaScannerClearMessage>(OnClear);
        });
    }

    private void OnUiOpen(Entity<PortableDnaScannerComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        UpdateUi(ent);
    }

    private void OnAfterInteract(Entity<PortableDnaScannerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Target is not { } target || !TryComp<DnaModifierComponent>(target, out var dna))
            return;

        ent.Comp.ScannedEntity = target;
        ent.Comp.Sample = new EnzymeInfo
        {
            SampleName = MetaData(target).EntityName,
            Identifier = dna.UniqueIdentifiers?.Clone(dna.UniqueIdentifiers),
            Info = dna.EnzymesPrototypes?.Select(static x => (EnzymesPrototypeInfo)x.Clone()).ToList()
        };
        ent.Comp.SampleDna = TryComp<DnaComponent>(target, out var forensic) && forensic.DNA != null
            ? GeneticSequence.ToNitrogenBases(forensic.DNA)
            : string.Empty;
        Dirty(ent);
        UpdateUi(ent);
        _ui.TryOpenUi(ent.Owner, PortableDnaScannerUiKey.Key, args.User);
    }

    private void OnSave(Entity<PortableDnaScannerComponent> ent, ref PortableDnaScannerSaveMessage args)
    {
        if (ent.Comp.Sample == null || !_slots.TryGetSlot(ent.Owner, SharedDnaModifier.DiskSlotName, out var slot) || slot.Item is not { } disk)
            return;

        var sample = (EnzymeInfo) ent.Comp.Sample.Clone();
        if (args.Kind == PortableDnaSampleKind.Unique)
            sample.Info = null;
        else if (args.Kind == PortableDnaSampleKind.Structural)
            sample.Identifier = null;
        _dnaModifier.TrySaveInDisk(disk, sample);
        UpdateUi(ent);
    }

    private void OnClear(Entity<PortableDnaScannerComponent> ent, ref PortableDnaScannerClearMessage args)
    {
        ent.Comp.ScannedEntity = null;
        ent.Comp.Sample = null;
        ent.Comp.SampleDna = string.Empty;
        Dirty(ent);
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<PortableDnaScannerComponent> ent)
    {
        var hasDisk = _slots.TryGetSlot(ent.Owner, SharedDnaModifier.DiskSlotName, out var slot) && slot.Item != null;
        _ui.SetUiState(ent.Owner, PortableDnaScannerUiKey.Key,
            new PortableDnaScannerState(ent.Comp.Sample?.SampleName ?? string.Empty, ent.Comp.SampleDna, ent.Comp.Sample, hasDisk));
    }
}
