using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Genetics;
using Content.Shared.Genetics.Systems;
using Content.Shared.Genetics.UI;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Forensics.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Genetics.System;

/// <summary>
/// Read-only handheld view of the DNA modifier. It deliberately has its own
/// target instead of pretending to be a medical scanner, so using it never
/// edits or replaces the scanned entity.
/// </summary>
public sealed class PortableGeneticAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly DnaClientSystem _dnaClient = default!;
    [Dependency] private readonly DnaModifierSystem _dnaModifier = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedDnaModifierSystem _sharedDna = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, AfterActivatableUIOpenEvent>(OnOpen);
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, ItemSlotButtonPressedEvent>(OnSlotPressed);
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, DnaModifierConsoleSaveServerEvent>(OnSave);
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, DnaModifierConsoleExportOnDiskEvent>(OnExportToDisk);
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, DnaModifierConsoleExportFromDiskEvent>(OnExportFromDisk);
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, DnaModifierConsoleClearDiskEvent>(OnClearDisk);
    }

    private void OnAfterInteract(Entity<PortableGeneticAnalyzerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<DnaModifierComponent>(target))
            return;

        ent.Comp.Target = target;
        Update(ent.Owner);
        _ui.OpenUi(ent.Owner, DnaModifierUiKey.Portable, args.User);
        args.Handled = true;
    }

    private void OnOpen(Entity<PortableGeneticAnalyzerComponent> ent, ref AfterActivatableUIOpenEvent args)
        => Update(ent.Owner);

    private void OnSlotPressed(Entity<PortableGeneticAnalyzerComponent> ent, ref ItemSlotButtonPressedEvent args)
    {
        if (args.SlotId != SharedDnaModifier.DiskSlotName)
            return;

        if (_itemSlots.TryGetSlot(ent.Owner, SharedDnaModifier.DiskSlotName, out var slot))
            _itemSlots.TryEject(ent.Owner, slot, null, out _, true);
        Update(ent.Owner);
    }

    private bool TryGetScan(EntityUid uid, out Entity<DnaModifierComponent> target)
    {
        target = default;
        if (!TryComp<PortableGeneticAnalyzerComponent>(uid, out var analyzer) || analyzer.Target is not { } targetUid)
            return false;
        if (!EntityManager.EntityExists(targetUid) || !TryComp(targetUid, out DnaModifierComponent? dna))
            return false;
        target = (targetUid, dna);
        return true;
    }

    private void OnSave(Entity<PortableGeneticAnalyzerComponent> ent, ref DnaModifierConsoleSaveServerEvent args)
    {
        if (args.CurrentSection is < 1 or > 3 || !TryGetScan(ent.Owner, out var target) ||
            !TryComp<DnaClientComponent>(ent.Owner, out var client))
            return;

        var data = new EnzymeInfo
        {
            SampleName = MetaData(target).EntityName,
            Identifier = _dnaModifier.CloneUniqueIdentifiers(target.Comp.UniqueIdentifiers),
            Info = _dnaModifier.CloneEnzymesPrototypes(target.Comp.EnzymesPrototypes)
        };
        _dnaClient.TryAddToBuffer((ent.Owner, client), args.CurrentSection, data);
        Update(ent.Owner);
    }

    private void OnExportToDisk(Entity<PortableGeneticAnalyzerComponent> ent, ref DnaModifierConsoleExportOnDiskEvent args)
    {
        if (args.Index is < 1 or > 3 || !TryComp<DnaClientComponent>(ent.Owner, out var client) ||
            !_dnaClient.TryGetBufferData((ent.Owner, client), args.Index, out var data) || data == null ||
            !_itemSlots.TryGetSlot(ent.Owner, SharedDnaModifier.DiskSlotName, out var slot) || slot.Item is not { } disk)
            return;

        _sharedDna.TrySaveInDisk(disk, data);
        Update(ent.Owner);
    }

    private void OnExportFromDisk(Entity<PortableGeneticAnalyzerComponent> ent, ref DnaModifierConsoleExportFromDiskEvent args)
    {
        if (args.Index is < 1 or > 3 || !TryComp<DnaClientComponent>(ent.Owner, out var client) ||
            !_itemSlots.TryGetSlot(ent.Owner, SharedDnaModifier.DiskSlotName, out var slot) || slot.Item is not { } disk ||
            !_sharedDna.TryGetDataFromDisk(disk, out var data) || data == null)
            return;

        _dnaClient.TryAddToBufferDisk((ent.Owner, client), args.Index, data);
        Update(ent.Owner);
    }

    private void OnClearDisk(Entity<PortableGeneticAnalyzerComponent> ent, ref DnaModifierConsoleClearDiskEvent args)
    {
        if (_itemSlots.TryGetSlot(ent.Owner, SharedDnaModifier.DiskSlotName, out var slot) && slot.Item is { } disk)
            _sharedDna.TryClearDiskData(disk);
        Update(ent.Owner);
    }

    private void Update(EntityUid uid)
    {
        if (!_ui.HasUi(uid, DnaModifierUiKey.Portable))
            return;

        var state = BuildState(uid);
        _ui.SetUiState(uid, DnaModifierUiKey.Portable, state);
    }

    private DnaModifierBoundUserInterfaceState BuildState(EntityUid analyzer)
    {
        EntityUid? target = null;
        UniqueIdentifiersData? unique = null;
        List<EnzymesPrototypeInfo>? enzymes = null;
        var name = string.Empty;
        var status = string.Empty;
        var species = string.Empty;
        var dna = string.Empty;
        var health = 1f;

        if (TryGetScan(analyzer, out var scanned))
        {
            target = scanned.Owner;
            unique = _dnaModifier.CloneUniqueIdentifiers(scanned.Comp.UniqueIdentifiers);
            enzymes = _dnaModifier.CloneEnzymesPrototypes(scanned.Comp.EnzymesPrototypes);
            name = MetaData(scanned).EntityName;
            status = string.Empty;
            if (TryComp<HumanoidAppearanceComponent>(scanned, out var humanoid))
                species = humanoid.Species.ToString();
            if (TryComp<DnaComponent>(scanned, out var dnaComp))
                dna = GeneticSequence.ToNitrogenBases(dnaComp.DNA ?? string.Empty);
        }

        var genes = new List<GeneticGeneState>();
        foreach (var gene in enzymes?.OrderBy(g => g.EnzymesPrototypeId == "Species") ?? Enumerable.Empty<EnzymesPrototypeInfo>())
        {
            var label = gene.EnzymesPrototypeId;
            if (_prototypes.TryIndex<StructuralEnzymesPrototype>(label, out var proto))
                label = string.IsNullOrWhiteSpace(proto.Abbreviation) ? proto.Name : proto.Abbreviation;
            genes.Add(new GeneticGeneState { Number = gene.Order, Name = label, Discovered = true, Active = gene.Active });
        }

        EnzymeInfo? diskData = null;
        var hasDisk = false;
        if (_itemSlots.TryGetSlot(analyzer, SharedDnaModifier.DiskSlotName, out var diskSlot) && diskSlot.Item is { } disk)
        {
            hasDisk = true;
            _sharedDna.TryGetDataFromDisk(disk, out diskData);
        }

        var buffers = new Dictionary<int, EnzymeInfo?>();
        if (TryComp<DnaClientComponent>(analyzer, out var client))
            for (var i = 1; i <= 3; i++)
                if (_dnaClient.TryGetBufferData((analyzer, client), i, out var buffer))
                    buffers[i] = buffer;

        return new DnaModifierBoundUserInterfaceState(
            GetNetEntity(analyzer), unique, enzymes, diskData, name, status, dna, species,
            health, 0, false, null, true, hasDisk, buffers, null, TimeSpan.Zero, TimeSpan.Zero,
            target is { } body ? GetNetEntity(body) : null, portable: true)
        {
            Genes = genes
        };
    }
}
