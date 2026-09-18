using System.Linq;
using Robust.Shared.Serialization;

namespace Content.Shared.Genetics;

[Serializable, NetSerializable]
public sealed class EnzymeInfo
{
    public string SampleName { get; set; } = string.Empty;
    public UniqueIdentifiersData? Identifier { get; set; }
    public List<EnzymesPrototypeInfo>? Info { get; set; }

    /// <summary>
    /// True when this sample represents a complete EI captured by the genetics computer,
    /// rather than a legacy U.I./S.E.-only transfer sample.
    /// </summary>
    public bool IsFullGeneticProfile { get; set; }

    /// <summary>
    /// Server-captured identity of the donor. Unlike SampleName this is not changed when
    /// a buffer is renamed and is never supplied by the client during application.
    /// </summary>
    public string? GeneticIdentityName { get; set; }

    /// <summary>
    /// Forensics DNA captured with the EI, when the donor has DnaComponent.
    /// </summary>
    public string? Dna { get; set; }

    /// <summary>
    /// Donor species identifier for profile information/compatibility display.
    /// Applying EI does not blindly replace the receiver's species.
    /// </summary>
    public string? SpeciesId { get; set; }

    /// <summary>
    /// Trauma mutation state belonging to the donor organism. Discovery is deliberately
    /// not stored here because it is global genetics knowledge, not organism state.
    /// </summary>
    public bool HasTraumaMutationState { get; set; }
    public List<string>? TraumaActiveMutations { get; set; }
    public List<string>? TraumaDormantMutations { get; set; }

    public object Clone()
    {
        return new EnzymeInfo
        {
            SampleName = SampleName,
            Identifier = Identifier != null ? Identifier.Clone(Identifier) : null,
            Info = Info?.Select(e => (EnzymesPrototypeInfo)e.Clone()).ToList(),
            IsFullGeneticProfile = IsFullGeneticProfile,
            GeneticIdentityName = GeneticIdentityName,
            Dna = Dna,
            SpeciesId = SpeciesId,
            HasTraumaMutationState = HasTraumaMutationState,
            TraumaActiveMutations = TraumaActiveMutations?.ToList(),
            TraumaDormantMutations = TraumaDormantMutations?.ToList()
        };
    }
}

[Serializable, NetSerializable]
public sealed class EnzymesPrototypeInfo
{
    public string EnzymesPrototypeId { get; set; } = string.Empty;
    public string[] HexCode { get; set; } = new[] { "0", "0", "0" };
    public int Order { get; set; } = default!;

    public object Clone()
    {
        return new EnzymesPrototypeInfo
        {
            EnzymesPrototypeId = EnzymesPrototypeId,
            HexCode = (string[])HexCode.Clone(),
            Order = Order
        };
    }
}

