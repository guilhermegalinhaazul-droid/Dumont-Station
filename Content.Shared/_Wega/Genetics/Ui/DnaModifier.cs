using Content.Shared.Chemistry;
using Robust.Shared.Serialization;

namespace Content.Shared.Genetics.UI;

public sealed class SharedDnaModifier
{
    public const string OccupantSlotName = "scanner-bodyContainer";
    public const string DiskSlotName = "diskSlot";
    public const string InputSlotName = "beakerSlot";
    public const string SolutionSlotName = "beaker";
}

[Serializable, NetSerializable]
public sealed class GeneCatalogEntry
{
    /// <summary>
    /// Stable identity shared by equivalent Wega and Trauma genes.
    /// </summary>
    public string CanonicalKey { get; set; } = string.Empty;

    /// <summary>
    /// ID used by the catalog UI. Wega wins when both systems represent the same gene.
    /// </summary>
    public string GeneId { get; set; } = string.Empty;

    public string GeneName { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;

    /// <summary>
    /// Wega structural-enzyme prototype backing this entry, when present.
    /// </summary>
    public string? WegaGeneId { get; set; }

    /// <summary>
    /// Trauma mutation prototype backing this entry, when present.
    /// </summary>
    public string? TraumaMutationId { get; set; }

    public bool Discovered { get; set; }
    public bool Active { get; set; }
    public bool Available { get; set; }

    /// <summary>
    /// True when the currently scanned genome contains a sequence for this Trauma mutation.
    /// This is independent from discovery and general gene availability.
    /// </summary>
    public bool CanSequence { get; set; }
}

[Serializable, NetSerializable]
public sealed class DnaModifierBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly NetEntity Console;
    public readonly UniqueIdentifiersData? Unique;
    public readonly List<EnzymesPrototypeInfo>? Enzymes;
    public readonly EnzymeInfo? Enzyme;
    public readonly string? ScannerBodyInfo;
    public readonly string? ScannerBodyStatus;
    public readonly string? ScannerBodyDna;
    public readonly string? ScannerSpecies;
    public readonly float ScannerBodyHealth;
    public readonly float ScannerBodyRadiation;
    public readonly bool ScannerHasBeaker;
    public readonly ContainerInfo? InputContainerInfo;
    public readonly bool ScannerInRange;
    public readonly bool HasDisk;
    public readonly Dictionary<int, EnzymeInfo?> Buffers;
    public readonly List<GeneCatalogEntry>? GeneCatalog;
    public readonly TimeSpan InjectorCooldownRemaining;
    public readonly TimeSpan SubjectInjectCooldownRemaining;
    public DnaModifierBoundUserInterfaceState(
        NetEntity console,
        UniqueIdentifiersData?
        unique,
        List<EnzymesPrototypeInfo>?
        enzymes,
        EnzymeInfo? enzyme,
        string? scannerBodyInfo,
        string? scannerBodyStatus,
        string? scannerBodyDna,
        string? scannerSpecies,
        float scannerBodyHealth,
        float scannerBodyRadiation,
        bool scannerHasBeaker,
        ContainerInfo? inputContainerInfo,
        bool scannerInRange,
        bool hasDisk,
        Dictionary<int, EnzymeInfo?> buffers,
        List<GeneCatalogEntry>? geneCatalog,
        TimeSpan injectorCooldownRemaining,
        TimeSpan subjectInjectCooldownRemaining)
    {
        Console = console;
        Unique = unique;
        Enzymes = enzymes;
        Enzyme = enzyme;
        ScannerBodyInfo = scannerBodyInfo;
        ScannerBodyStatus = scannerBodyStatus;
        ScannerBodyDna = scannerBodyDna;
        ScannerSpecies = scannerSpecies;
        ScannerBodyHealth = scannerBodyHealth;
        ScannerBodyRadiation = scannerBodyRadiation;
        ScannerHasBeaker = scannerHasBeaker;
        InputContainerInfo = inputContainerInfo;
        ScannerInRange = scannerInRange;
        HasDisk = hasDisk;
        Buffers = buffers;
        GeneCatalog = geneCatalog;
        InjectorCooldownRemaining = injectorCooldownRemaining;
        SubjectInjectCooldownRemaining = subjectInjectCooldownRemaining;
    }
}

[Serializable, NetSerializable]
public enum DnaModifierUiKey
{
    Key,
}

public enum DnaModifierReagentAmount
{
    U1 = 1,
    U5 = 5,
    U10 = 10,
    U25 = 25,
    U50 = 50,
    U100 = 100,
    All,
}
