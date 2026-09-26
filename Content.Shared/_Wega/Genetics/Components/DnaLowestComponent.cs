namespace Content.Shared.Genetics;

[RegisterComponent]
public sealed partial class DnaLowestComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Parent = default!;

    // DNA captured before the species gene creates the temporary lower form.
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public UniqueIdentifiersData? OriginalUniqueIdentifiers;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public List<EnzymesPrototypeInfo>? OriginalEnzymesPrototypes;
}
