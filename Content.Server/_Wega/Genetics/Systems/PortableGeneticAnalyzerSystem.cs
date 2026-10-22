using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Genetics;
using Content.Shared.Genetics.Systems;
using Content.Shared.Genetics.UI;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Forensics.Components;
using Content.Shared.DoAfter;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
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
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedDnaModifierSystem _sharedDna = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly DnaModifierConsoleSystem _sequencing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        // Reuse the already network-registered genetics DoAfter event. A custom
        // event here can leave the server serializer without a type entry when
        // an old content assembly is still present in the release output.
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, DnaInjectorDoAfterEvent>(OnScanComplete);
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, AfterActivatableUIOpenEvent>(OnOpen);
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, ItemSlotButtonPressedEvent>(OnSlotPressed);
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, DnaModifierConsoleSaveServerEvent>(OnSave);
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, DnaModifierConsoleClearBufferEvent>(OnClearBuffer);
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, DnaModifierConsoleExportOnDiskEvent>(OnExportToDisk);
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, DnaModifierConsoleExportFromDiskEvent>(OnExportFromDisk);
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, DnaModifierConsoleClearDiskEvent>(OnClearDisk);
        SubscribeLocalEvent<PortableGeneticAnalyzerComponent, GeneticSelectMessage>(OnSelectGene);
    }

    private void OnAfterInteract(Entity<PortableGeneticAnalyzerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasDnaSample(target))
            return;

        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(0.8),
                new DnaInjectorDoAfterEvent(), ent.Owner, target: target)
            {
                NeedHand = true,
                BreakOnMove = true,
                BreakOnDamage = true,
            }))
            return;

        args.Handled = true;
    }

    private void OnScanComplete(Entity<PortableGeneticAnalyzerComponent> ent, ref DnaInjectorDoAfterEvent args)
    {
        if (args.Cancelled || !args.Target.HasValue || !HasDnaSample(args.Target.Value))
            return;

        ent.Comp.Target = args.Target.Value;
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/Medical/healthscanner.ogg"), ent.Owner);
        _ui.OpenUi(ent.Owner, DnaModifierUiKey.Portable, args.User);
        // Open the UI first, then send the state for the newly scanned target.
        // Updating before OpenUi is a no-op on the first scan and can leave the
        // client displaying the previous target until it is scanned twice.
        Update(ent.Owner);
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

    private bool HasDnaSample(EntityUid uid)
    {
        if (HasComp<DnaModifierComponent>(uid))
            return true;

        if (TryComp<DnaComponent>(uid, out var direct) && IsUsableDna(direct.DNA))
            return true;

        if (TryComp<ForensicsComponent>(uid, out var forensics) && forensics.DNAs.Any(entry => IsUsableDna(entry.Item1)))
            return true;

        return HasDnaInSolutions(uid);
    }

    private bool IsUsableDna(string? dna)
        => !string.IsNullOrWhiteSpace(dna) && dna != Loc.GetString("forensics-dna-unknown");

    private bool HasDnaInSolutions(EntityUid uid)
    {
        if (!TryComp<SolutionContainerManagerComponent>(uid, out var manager))
            return false;

        foreach (var containerName in manager.Containers)
        {
            if (!_solutions.TryGetSolution(uid, containerName, out _, out var solution))
                continue;
            foreach (var reagent in solution.Contents)
            {
                if (reagent.Reagent.Data?.OfType<DnaData>().Any(data => IsUsableDna(data.DNA)) == true)
                    return true;
            }
        }

        return false;
    }

    private bool TryGetSampleDna(EntityUid uid, out string dna)
    {
        if (TryComp<DnaComponent>(uid, out var direct) && IsUsableDna(direct.DNA))
        {
            dna = direct.DNA!;
            return true;
        }

        if (TryComp<ForensicsComponent>(uid, out var forensics) && forensics.DNAs.Any(entry => IsUsableDna(entry.Item1)))
        {
            // Gibbed organs and blood traces retain the source DNA in the
            // forensics record. Prefer the newest trace when several exist.
            dna = forensics.DNAs.Where(entry => IsUsableDna(entry.Item1))
                .OrderByDescending(entry => entry.Item2).First().Item1;
            return true;
        }

        if (TryComp<SolutionContainerManagerComponent>(uid, out var manager))
        {
            foreach (var containerName in manager.Containers)
            {
                if (!_solutions.TryGetSolution(uid, containerName, out _, out var solution))
                    continue;
                foreach (var reagent in solution.Contents)
                {
                    var sample = reagent.Reagent.Data?.OfType<DnaData>().FirstOrDefault(data => IsUsableDna(data.DNA));
                    if (sample is not null)
                    {
                        dna = sample.DNA;
                        return true;
                    }
                }
            }
        }

        dna = string.Empty;
        return false;
    }

    private bool TryGetGeneticSample(EntityUid uid, out EnzymeInfo sample, out string dna)
    {
        sample = default!;
        dna = string.Empty;

        if (TryComp<DnaModifierComponent>(uid, out var modifier))
        {
            sample = new EnzymeInfo
            {
                SampleName = MetaData(uid).EntityName,
                Identifier = _dnaModifier.CloneUniqueIdentifiers(modifier.UniqueIdentifiers),
                Info = _dnaModifier.CloneEnzymesPrototypes(modifier.EnzymesPrototypes)
            };
            if (TryComp<DnaComponent>(uid, out var directDna) && IsUsableDna(directDna.DNA))
                dna = directDna.DNA!;
            return sample.Identifier is not null || sample.Info is { Count: > 0 };
        }

        if (!TryGetSampleDna(uid, out dna))
            return false;

        if (TryComp<ForensicsComponent>(uid, out var forensics) &&
            forensics.GeneticSamples.TryGetValue(dna, out var forensicSample))
        {
            sample = (EnzymeInfo) forensicSample.Clone();
            return true;
        }

        if (TryGetSolutionGeneticSample(uid, dna, out var solutionSample))
        {
            sample = solutionSample;
            return true;
        }

        // Older blood and forensic traces only carry the DNA string. If their
        // source is still present, recover its current Wega profile by DNA.
        var query = EntityQueryEnumerator<DnaComponent, DnaModifierComponent>();
        while (query.MoveNext(out var source, out var dnaComp, out var sourceModifier))
        {
            if (dnaComp.DNA != dna)
                continue;

            sample = new EnzymeInfo
            {
                SampleName = MetaData(source).EntityName,
                Identifier = _dnaModifier.CloneUniqueIdentifiers(sourceModifier.UniqueIdentifiers),
                Info = _dnaModifier.CloneEnzymesPrototypes(sourceModifier.EnzymesPrototypes)
            };
            return sample.Identifier is not null || sample.Info is { Count: > 0 };
        }

        return false;
    }

    private bool TryGetSolutionGeneticSample(EntityUid uid, string dna, out EnzymeInfo sample)
    {
        sample = default!;
        if (!TryComp<SolutionContainerManagerComponent>(uid, out var manager))
            return false;

        foreach (var containerName in manager.Containers)
        {
            if (!_solutions.TryGetSolution(uid, containerName, out _, out var solution))
                continue;

            foreach (var reagent in solution.Contents)
            {
                var data = reagent.Reagent.Data?.OfType<DnaData>().FirstOrDefault(entry => entry.DNA == dna);
                if (data?.GeneticSample is null)
                    continue;

                sample = (EnzymeInfo) data.GeneticSample.Clone();
                return true;
            }
        }

        return false;
    }

    private void OnSave(Entity<PortableGeneticAnalyzerComponent> ent, ref DnaModifierConsoleSaveServerEvent args)
    {
        if (args.CurrentSection is < 1 or > 3 || ent.Comp.Target is not { } targetUid ||
            !TryGetGeneticSample(targetUid, out var profile, out _) ||
            !TryComp<DnaClientComponent>(ent.Owner, out var client))
            return;

        var data = new EnzymeInfo
        {
            SampleName = string.IsNullOrWhiteSpace(profile.SampleName) ? MetaData(targetUid).EntityName : profile.SampleName
        };
        switch (args.CurrentType)
        {
            case 1: // EU only
                data.Identifier = _dnaModifier.CloneUniqueIdentifiers(profile.Identifier);
                break;
            case 2: // EU + EE
                data.Identifier = _dnaModifier.CloneUniqueIdentifiers(profile.Identifier);
                data.Info = _dnaModifier.CloneEnzymesPrototypes(profile.Info);
                break;
            case 3: // EE only
                data.Info = _dnaModifier.CloneEnzymesPrototypes(profile.Info);
                break;
            default:
                return;
        }

        if ((args.CurrentType == 1 && data.Identifier is null) ||
            (args.CurrentType == 2 && (data.Identifier is null || data.Info is not { Count: > 0 })) ||
            (args.CurrentType == 3 && data.Info is not { Count: > 0 }))
            return;

        _dnaClient.TryAddToBuffer((ent.Owner, client), args.CurrentSection, data);
        Update(ent.Owner);
    }

    private void OnClearBuffer(Entity<PortableGeneticAnalyzerComponent> ent, ref DnaModifierConsoleClearBufferEvent args)
    {
        if (args.Index is < 1 or > 3 || !TryComp<DnaClientComponent>(ent.Owner, out var client))
            return;

        _dnaClient.TryClearBuffer((ent.Owner, client), args.Index);
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

    private void OnSelectGene(Entity<PortableGeneticAnalyzerComponent> ent, ref GeneticSelectMessage args)
    {
        if (TryGetScan(ent.Owner, out var target))
            _sequencing.TrySelectPortableGene(ent.Owner, target.Owner, args.Actor, args.Number);
        Update(ent.Owner);
    }

    private void OnSubmitGene(Entity<PortableGeneticAnalyzerComponent> ent, ref GeneticSubmitMessage args)
    {
        if (TryGetScan(ent.Owner, out var target))
            _sequencing.TrySubmitPortableGene(ent.Owner, target.Owner, args.Actor, args.Token, args.Answer);
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
            name = MetaData(scanned).EntityName;
            if (TryGetGeneticSample(scanned.Owner, out var profile, out var scannedDna))
            {
                unique = profile.Identifier;
                enzymes = profile.Info;
                dna = GeneticSequence.ToNitrogenBases(scannedDna);
                if (!string.IsNullOrWhiteSpace(profile.SampleName))
                    name = profile.SampleName;
            }
            status = string.Empty;
            if (TryComp<HumanoidAppearanceComponent>(scanned, out var humanoid))
                species = humanoid.Species.ToString();
            if (string.IsNullOrWhiteSpace(dna) && TryComp<DnaComponent>(scanned, out var dnaComp))
                dna = GeneticSequence.ToNitrogenBases(dnaComp.DNA ?? string.Empty);
        }
        else if (TryComp<PortableGeneticAnalyzerComponent>(analyzer, out var portable) &&
                 portable.Target is { } sample && TryGetSampleDna(sample, out var sampleDna))
        {
            target = sample;
            name = MetaData(sample).EntityName;
            dna = GeneticSequence.ToNitrogenBases(sampleDna);
            if (TryGetGeneticSample(sample, out var profile, out _))
            {
                unique = profile.Identifier;
                enzymes = profile.Info;
                if (!string.IsNullOrWhiteSpace(profile.SampleName))
                    name = profile.SampleName;
            }
        }

        var genes = new List<GeneticGeneState>();
        foreach (var gene in enzymes?.OrderBy(g => g.EnzymesPrototypeId == "Species") ?? Enumerable.Empty<EnzymesPrototypeInfo>())
        {
            var discovered = _sequencing.IsGeneDiscovered(gene.EnzymesPrototypeId);
            var label = discovered ? gene.EnzymesPrototypeId : "???";
            if (discovered && _prototypes.TryIndex<StructuralEnzymesPrototype>(label, out var proto))
                label = string.IsNullOrWhiteSpace(proto.Abbreviation) ? proto.Name : proto.Abbreviation;
            genes.Add(new GeneticGeneState { Number = gene.Order, Name = label, Discovered = discovered, Active = gene.Active });
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

        var appearanceFields = unique is null
            ? new List<string>()
            : AppearanceGene.Fields.Where(field => field == AppearanceGene.Name ||
                AppearanceGene.Get(unique, field) is { Length: > 0 }).ToList();

        return new DnaModifierBoundUserInterfaceState(
            GetNetEntity(analyzer), unique, enzymes, diskData, name, status, dna, species,
            health, 0, false, null, true, hasDisk, buffers, null, TimeSpan.Zero, TimeSpan.Zero,
            target is { } body ? GetNetEntity(body) : null, portable: true)
        {
            Genes = genes,
            AppearanceFields = appearanceFields
            ,Puzzle = _sequencing.GetPortablePuzzle(analyzer, target)
        };
    }
}
